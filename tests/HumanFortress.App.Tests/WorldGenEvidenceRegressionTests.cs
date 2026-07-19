using System.Reflection;
using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Core.Determinism;
using HumanFortress.Core.World;
using HumanFortress.Simulation.Tiles;
using HumanFortress.WorldGen.Implementation;
using SadRogue.Primitives;
using SimulationWorld = HumanFortress.Simulation.World.World;

internal static class WorldGenEvidenceRegressionTests
{
    private const byte CavernMossBit = 1 << 3;
    private static readonly Lazy<FortressRuntimeContentSnapshot> RuntimeContent = new(LoadRuntimeContent);
    private static readonly Lazy<FortressEvidenceFixture> FortressFixture = new(CreateFortressEvidenceFixture);

    internal static void RunAll()
    {
        Console.WriteLine("=== WorldGen Evidence Regression Tests ===");
        TestFixedSeedWorldMapCanonicalHashesRepeatAndSeedsDiverge();
        TestWorldMapValuesAndMultiSeedDistributionStaySane();
        TestStageFailureFailsClosedAndStopsThePipeline();
        TestFortressCanonicalHashAndGeologyHandlesAreValid();
        TestGeneratedSurfaceTransitionMaskAndShaftOpeningsAreCoherent();
        TestGeneratedCavernFloorMaskIsConnectedInTwoDimensions();
        Console.WriteLine("=== WorldGen Evidence Regression Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestFixedSeedWorldMapCanonicalHashesRepeatAndSeedsDiverge()
    {
        uint[] seeds = { 1, 12_345, 99_999, 0xDEAD_BEEF };
        var distinctHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (uint seed in seeds)
        {
            var first = GenerateWorldMap(seed, width: 64, height: 48);
            var repeated = GenerateWorldMap(seed, width: 64, height: 48);
            string firstHash = BuildWorldMapCanonicalHash(first);
            string repeatedHash = BuildWorldMapCanonicalHash(repeated);

            RegressionAssert.True(
                first.Success
                && repeated.Success
                && firstHash.Length == 64
                && firstHash == repeatedHash,
                $"World-map seed {seed} did not reproduce its complete canonical output hash.");
            distinctHashes.Add(firstHash);
        }

        RegressionAssert.True(
            distinctHashes.Count == seeds.Length,
            "Different world-map seeds did not change the canonical generated output.");

        Console.WriteLine("[PASS] Multiple fixed world-map seeds reproduce complete canonical hashes and remain seed-sensitive");
    }

    private static void TestWorldMapValuesAndMultiSeedDistributionStaySane()
    {
        uint[] seeds = { 7, 11, 29, 101, 503, 2_027, 65_537, 900_001 };
        var biomeCounts = new Dictionary<ushort, int>();
        int tileCount = 0;
        float minimumElevation = float.PositiveInfinity;
        float maximumElevation = float.NegativeInfinity;
        bool allValuesValid = true;

        foreach (uint seed in seeds)
        {
            var result = GenerateWorldMap(seed, width: 48, height: 48);
            RegressionAssert.True(result.Success, $"World-map distribution seed {seed} failed: {result.ErrorMessage}");

            for (int y = 0; y < result.Tiles.GetLength(1); y++)
            for (int x = 0; x < result.Tiles.GetLength(0); x++)
            {
                var tile = result.Tiles[x, y];
                allValuesValid &= IsNormalizedFinite(tile.Elevation)
                    && IsNormalizedFinite(tile.Temperature)
                    && IsNormalizedFinite(tile.Rainfall)
                    && IsNormalizedFinite(tile.Drainage);
                minimumElevation = Math.Min(minimumElevation, tile.Elevation);
                maximumElevation = Math.Max(maximumElevation, tile.Elevation);
                biomeCounts[tile.BiomeId] = biomeCounts.GetValueOrDefault(tile.BiomeId) + 1;
                tileCount++;
            }
        }

        int largestBiome = biomeCounts.Values.Max();
        RegressionAssert.True(
            allValuesValid
            && minimumElevation < 0.45f
            && maximumElevation > 0.55f
            && maximumElevation - minimumElevation > 0.25f
            && biomeCounts.Count >= 5
            && largestBiome < tileCount * 95L / 100L,
            $"Multi-seed WorldGen distribution collapsed or produced invalid values: "
            + $"elevation=[{minimumElevation},{maximumElevation}], biomes={biomeCounts.Count}, "
            + $"largest={largestBiome}/{tileCount}.");

        Console.WriteLine("[PASS] Multi-seed world maps keep finite normalized fields and broad biome/elevation coverage");
    }

    private static void TestStageFailureFailsClosedAndStopsThePipeline()
    {
        var diagnostics = new RecordingDiagnosticSink();
        var generator = new WorldGenerator(diagnostics);
        var laterStage = new RecordingWorldGenStage();
        var stagesField = typeof(WorldGenerator).GetField("_stages", BindingFlags.Instance | BindingFlags.NonPublic);
        var stages = stagesField?.GetValue(generator) as List<IWorldGenStage>;
        RegressionAssert.True(stages != null, "WorldGenerator stage list was unavailable for failure-path behavior injection.");

        stages!.Clear();
        stages.Add(new ThrowingWorldGenStage());
        stages.Add(laterStage);
        var progress = new List<string>();
        generator.ProgressChanged += (stage, _) => progress.Add(stage);

        var result = generator.Generate(new WorldParams
        {
            Seed = 42,
            Width = 8,
            Height = 8,
            Name = "fail-closed-evidence"
        });

        RegressionAssert.True(
            !result.Success
            && !laterStage.Executed
            && result.ErrorMessage.Contains("Evidence.ThrowingStage", StringComparison.Ordinal)
            && result.ErrorMessage.Contains("intentional WorldGen stage failure", StringComparison.Ordinal)
            && diagnostics.Events.Any(static entry => entry.Message.Contains("Evidence.ThrowingStage", StringComparison.Ordinal))
            && !progress.Contains("Complete", StringComparer.Ordinal),
            "WorldGenerator continued after a stage failure or reported a failed pipeline as complete.");

        Console.WriteLine("[PASS] WorldGenerator stage failures fail closed and stop later stages");
    }

    private static void TestFortressCanonicalHashAndGeologyHandlesAreValid()
    {
        var fixture = FortressFixture.Value;
        string baselineHash = BuildFortressCanonicalHash(fixture.Baseline);
        string repeatedHash = BuildFortressCanonicalHash(fixture.Repeated);
        string changedSeedHash = BuildFortressCanonicalHash(fixture.ChangedSeed);
        int invalidHandles = 0;
        int airCells = 0;
        int floorCells = 0;
        int wallCells = 0;

        foreach (var cell in EnumerateFortressCells(fixture.Baseline))
        {
            var geology = fixture.Geology.GetGeologyByHandle(cell.Handle);
            if (geology == null || fixture.Geology.GetGeologyHandle(geology.Id) != cell.Handle)
            {
                invalidHandles++;
                continue;
            }

            if (!Enum.TryParse<TerrainKind>(geology.TerrainBits.Kind, out var kind))
            {
                invalidHandles++;
                continue;
            }

            switch (kind)
            {
                case TerrainKind.OpenNoFloor:
                    airCells++;
                    break;
                case TerrainKind.OpenWithFloor:
                    floorCells++;
                    break;
                case TerrainKind.SolidWall:
                    wallCells++;
                    break;
            }
        }

        RegressionAssert.True(
            baselineHash == repeatedHash
            && baselineHash != changedSeedHash
            && invalidHandles == 0
            && airCells > 0
            && floorCells > 0
            && wallCells > 0,
            $"Generated fortress output was unstable or contained invalid geology handles: "
            + $"invalid={invalidHandles}, air={airCells}, floor={floorCells}, wall={wallCells}.");

        Console.WriteLine("[PASS] Fortress canonical output is deterministic, seed-sensitive, and uses reversible geology handles");
    }

    private static void TestGeneratedSurfaceTransitionMaskAndShaftOpeningsAreCoherent()
    {
        var world = FortressFixture.Value.FilledWorld;
        int size = world.SizeInTiles;
        var surfaceZ = new int[size, size];
        int rampCount = 0;
        int surfaceColumnCount = 0;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            surfaceZ[x, y] = FindMarkedTopSurface(world, x, y);
            if (surfaceZ[x, y] < 0)
                continue;

            surfaceColumnCount++;
            var surfaceKind = world.GetTile(x, y, surfaceZ[x, y])?.Kind;
            RegressionAssert.True(
                surfaceKind is TerrainKind.OpenWithFloor or TerrainKind.Ramp,
                $"Generated surface marker at {x},{y},{surfaceZ[x, y]} is not a floor or ramp.");
            if (surfaceKind == TerrainKind.Ramp)
                rampCount++;
        }

        int shaftOpeningCount = 0;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            if (surfaceZ[x, y] >= 0)
                continue;

            RegressionAssert.True(
                IsEdgeCavernShaftAperture(world, surfaceZ, x, y),
                $"Generated column {x},{y} has neither a marked surface nor a coherent edge shaft aperture and cavern landing.");
            shaftOpeningCount++;
        }

        var visited = new bool[size, size];
        var queue = new Queue<(int X, int Y)>();
        var firstSurface = Enumerable.Range(0, size)
            .SelectMany(y => Enumerable.Range(0, size).Select(x => (X: x, Y: y)))
            .First(cell => surfaceZ[cell.X, cell.Y] >= 0);
        queue.Enqueue(firstSurface);
        visited[firstSurface.X, firstSurface.Y] = true;
        int visitedCount = 0;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            visitedCount++;
            foreach (var (dx, dy) in EightNeighbors)
            {
                int nextX = current.X + dx;
                int nextY = current.Y + dy;
                if (nextX < 0 || nextY < 0 || nextX >= size || nextY >= size || visited[nextX, nextY])
                    continue;
                if (surfaceZ[nextX, nextY] < 0)
                    continue;
                if (!HasSurfaceTransition(world, surfaceZ, current.X, current.Y, nextX, nextY))
                    continue;

                visited[nextX, nextY] = true;
                queue.Enqueue((nextX, nextY));
            }
        }

        RegressionAssert.True(
            surfaceColumnCount + shaftOpeningCount == size * size
            && shaftOpeningCount >= 2
            && rampCount > 0
            && visitedCount == surfaceColumnCount,
            $"Generated eight-neighbor surface transition mask is disconnected: "
            + $"visited={visitedCount}/{surfaceColumnCount}, ramps={rampCount}, shafts={shaftOpeningCount}.");

        Console.WriteLine(
            "[PASS] Generated surface/ramp mask is connected around edge shaft apertures; "
            + "shaft checks prove geometry, not 3D navigation");
    }

    private static void TestGeneratedCavernFloorMaskIsConnectedInTwoDimensions()
    {
        var fixture = FortressFixture.Value;
        var cavernCells = EnumerateFortressCells(fixture.Baseline)
            .Where(static cell => (cell.SurfaceBits & CavernMossBit) != 0)
            .Select(static cell => new GridCell(cell.GlobalX, cell.GlobalY, cell.Z))
            .ToHashSet();
        var layers = cavernCells.Select(static cell => cell.Z).Distinct().ToArray();
        bool allMarkedCellsAreFloors = cavernCells.All(cell =>
        {
            ushort handle = GetFortressCell(fixture.Baseline, cell.X, cell.Y, cell.Z).Handle;
            var geology = fixture.Geology.GetGeologyByHandle(handle);
            return geology != null
                && Enum.TryParse<TerrainKind>(geology.TerrainBits.Kind, out var kind)
                && kind == TerrainKind.OpenWithFloor;
        });

        RegressionAssert.True(
            cavernCells.Count > 0 && layers.Length == 1 && allMarkedCellsAreFloors,
            "Generated cavern evidence mask was empty, spanned multiple bands, or marked non-floor cells.");

        var visited = new HashSet<GridCell>();
        var queue = new Queue<GridCell>();
        var first = cavernCells
            .OrderBy(static cell => cell.Y)
            .ThenBy(static cell => cell.X)
            .First();
        visited.Add(first);
        queue.Enqueue(first);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var (dx, dy) in FourNeighbors)
            {
                var next = current with { X = current.X + dx, Y = current.Y + dy };
                if (cavernCells.Contains(next) && visited.Add(next))
                    queue.Enqueue(next);
            }
        }

        RegressionAssert.True(
            visited.Count == cavernCells.Count,
            $"Generated single-band cavern floor mask is not orthogonally connected in 2D: "
            + $"visited={visited.Count}/{cavernCells.Count}.");

        Console.WriteLine("[PASS] Generated moss-marked cavern floor mask is one orthogonally connected 2D component");
    }

    private static WorldGenResult GenerateWorldMap(uint seed, int width, int height)
    {
        return new WorldGenerator(new RecordingDiagnosticSink()).Generate(new WorldParams
        {
            Seed = seed,
            Width = width,
            Height = height,
            Name = $"evidence-{seed}"
        });
    }

    private static string BuildWorldMapCanonicalHash(WorldGenResult result)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("worldgen.world-map.output.v1");
            int width = result.Tiles.GetLength(0);
            int height = result.Tiles.GetLength(1);
            hash.AddInt32(width);
            hash.AddInt32(height);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var tile = result.Tiles[x, y];
                hash.AddInt32(x);
                hash.AddInt32(y);
                hash.AddInt32(tile.BiomeId);
                hash.AddInt32(BitConverter.SingleToInt32Bits(tile.Elevation));
                hash.AddInt32(BitConverter.SingleToInt32Bits(tile.Temperature));
                hash.AddInt32(BitConverter.SingleToInt32Bits(tile.Rainfall));
                hash.AddInt32(BitConverter.SingleToInt32Bits(tile.Drainage));
                hash.AddByte(tile.RiverClass);
                hash.AddBoolean(tile.HasAquifer);
                AddUInt16List(hash, tile.StoneSet);
                AddInt32List(hash, tile.LandmarkIds);
            }
        });
    }

    private static string BuildFortressCanonicalHash(FortressMap map)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("worldgen.fortress-map.output.v1");
            hash.AddInt32(map.Size);
            hash.AddInt32(map.MaxZ);
            foreach (var cell in EnumerateFortressCells(map))
            {
                hash.AddInt32(cell.GlobalX);
                hash.AddInt32(cell.GlobalY);
                hash.AddInt32(cell.Z);
                hash.AddInt32(cell.Handle);
                hash.AddByte(cell.SurfaceBits);
            }
        });
    }

    private static IEnumerable<FortressCell> EnumerateFortressCells(FortressMap map)
    {
        for (int z = 0; z < map.MaxZ; z++)
        for (int chunkY = 0; chunkY < map.Size; chunkY++)
        for (int localY = 0; localY < 32; localY++)
        for (int chunkX = 0; chunkX < map.Size; chunkX++)
        for (int localX = 0; localX < 32; localX++)
        {
            var chunk = map.GetChunk(chunkX, chunkY);
            yield return new FortressCell(
                chunkX * 32 + localX,
                chunkY * 32 + localY,
                z,
                chunk.GetGeologyHandle(localX, localY, z),
                chunk.GetSurfaceBits(localX, localY, z));
        }
    }

    private static FortressCell GetFortressCell(FortressMap map, int globalX, int globalY, int z)
    {
        var chunk = map.GetChunk(globalX / 32, globalY / 32);
        int localX = globalX % 32;
        int localY = globalY % 32;
        return new FortressCell(
            globalX,
            globalY,
            z,
            chunk.GetGeologyHandle(localX, localY, z),
            chunk.GetSurfaceBits(localX, localY, z));
    }

    private static int FindMarkedTopSurface(SimulationWorld world, int x, int y)
    {
        for (int z = world.MaxZ - 1; z >= 0; z--)
        {
            var tile = world.GetTile(x, y, z);
            if (tile.HasValue
                && tile.Value.HasMud
                && tile.Value.Kind is TerrainKind.OpenWithFloor or TerrainKind.Ramp)
                return z;
        }

        return -1;
    }

    private static bool IsEdgeCavernShaftAperture(
        SimulationWorld world,
        int[,] surfaceZ,
        int x,
        int y)
    {
        int size = world.SizeInTiles;
        int edgeDistance = Math.Min(Math.Min(x, size - 1 - x), Math.Min(y, size - 1 - y));
        if (edgeDistance > 3)
            return false;

        int lowestContiguousAirZ = world.MaxZ;
        for (int z = world.MaxZ - 1; z >= 0; z--)
        {
            var tile = world.GetTile(x, y, z);
            if (!tile.HasValue || tile.Value.Kind != TerrainKind.OpenNoFloor)
                break;
            if (tile.Value.SurfaceBits != 0)
                return false;

            lowestContiguousAirZ = z;
        }

        if (lowestContiguousAirZ <= 0 || lowestContiguousAirZ >= world.MaxZ)
            return false;

        bool penetratesLocalSurface = false;
        foreach (var (dx, dy) in EightNeighbors)
        {
            int neighborX = x + dx;
            int neighborY = y + dy;
            if (neighborX < 0 || neighborY < 0 || neighborX >= size || neighborY >= size)
                continue;

            int neighborSurfaceZ = surfaceZ[neighborX, neighborY];
            if (neighborSurfaceZ >= lowestContiguousAirZ + 2
                && world.GetTile(x, y, neighborSurfaceZ)?.Kind == TerrainKind.OpenNoFloor)
            {
                penetratesLocalSurface = true;
                break;
            }
        }

        bool hasCavernFloorLanding = IsMossFloor(world.GetTile(x, y, lowestContiguousAirZ - 1));
        return penetratesLocalSurface && hasCavernFloorLanding;
    }

    private static bool IsMossFloor(TileBase? tile)
    {
        return tile.HasValue
            && tile.Value.Kind == TerrainKind.OpenWithFloor
            && tile.Value.HasMoss;
    }

    private static bool HasSurfaceTransition(
        SimulationWorld world,
        int[,] surfaceZ,
        int firstX,
        int firstY,
        int secondX,
        int secondY)
    {
        int firstZ = surfaceZ[firstX, firstY];
        int secondZ = surfaceZ[secondX, secondY];
        if (firstZ == secondZ)
            return true;
        if (Math.Abs(firstZ - secondZ) != 1)
            return false;

        int lowerX = firstZ < secondZ ? firstX : secondX;
        int lowerY = firstZ < secondZ ? firstY : secondY;
        int lowerZ = Math.Min(firstZ, secondZ);
        return world.GetTile(lowerX, lowerY, lowerZ)?.Kind == TerrainKind.Ramp;
    }

    private static FortressEvidenceFixture CreateFortressEvidenceFixture()
    {
        var runtimeContent = RuntimeContent.Value;
        var generationContent = new FortressGenerationContent(
            runtimeContent.Geology,
            runtimeContent.MapgenTuningJson,
            runtimeContent.OreTuningJson,
            runtimeContent.CavernTuningJson);
        var worldTile = new WorldTile
        {
            BiomeId = (ushort)BiomeType.TemperateForest,
            Elevation = 0.55f,
            Temperature = 0.55f,
            Rainfall = 0.65f,
            Drainage = 0.5f,
            StoneSet = Array.Empty<ushort>(),
            LandmarkIds = Array.Empty<int>()
        };
        var diagnostics = new RecordingDiagnosticSink();
        var location = new Point(10, 10);
        var baseline = new FortressGenerator(2, worldTile, location, 123_456, generationContent, diagnostics).Generate();
        var repeated = new FortressGenerator(2, worldTile, location, 123_456, generationContent, diagnostics).Generate();
        var changedSeed = new FortressGenerator(2, worldTile, location, 654_321, generationContent, diagnostics).Generate();
        var filledWorld = new SimulationWorld(baseline.Size, baseline.MaxZ);
        baseline.FillWorld(filledWorld);

        return new FortressEvidenceFixture(
            baseline,
            repeated,
            changedSeed,
            filledWorld,
            runtimeContent.Geology);
    }

    private static FortressRuntimeContentSnapshot LoadRuntimeContent()
    {
        var loaded = FortressContentLoader.LoadStrict(
            AppContext.BaseDirectory,
            treatWarningsAsErrors: true);
        return loaded.CoreCatalogs == null
            ? FortressRuntimeContentSnapshotLoader.CaptureLoaded(
                loaded.StructuredRegistry,
                loaded.MechanicalIdentity,
                loaded.Professions,
                loaded.StockpilePresetDefinitions)
            : FortressRuntimeContentSnapshotLoader.ApplyCoreData(
                loaded.StructuredRegistry,
                loaded.CoreCatalogs.CoreData,
                loaded.MechanicalIdentity,
                loaded.Professions,
                loaded.StockpilePresetDefinitions);
    }

    private static bool IsNormalizedFinite(float value)
    {
        return float.IsFinite(value) && value >= 0f && value <= 1f;
    }

    private static void AddUInt16List(ReplayHashBuilder hash, IReadOnlyList<ushort>? values)
    {
        if (values == null)
        {
            hash.AddInt32(-1);
            return;
        }

        hash.AddInt32(values.Count);
        foreach (ushort value in values)
            hash.AddInt32(value);
    }

    private static void AddInt32List(ReplayHashBuilder hash, IReadOnlyList<int>? values)
    {
        if (values == null)
        {
            hash.AddInt32(-1);
            return;
        }

        hash.AddInt32(values.Count);
        foreach (int value in values)
            hash.AddInt32(value);
    }

    private static readonly (int X, int Y)[] FourNeighbors =
    {
        (0, -1),
        (1, 0),
        (0, 1),
        (-1, 0)
    };

    private static readonly (int X, int Y)[] EightNeighbors =
    {
        (0, -1),
        (1, -1),
        (1, 0),
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1)
    };

    private readonly record struct FortressCell(
        int GlobalX,
        int GlobalY,
        int Z,
        ushort Handle,
        byte SurfaceBits);

    private readonly record struct GridCell(int X, int Y, int Z);

    private sealed record FortressEvidenceFixture(
        FortressMap Baseline,
        FortressMap Repeated,
        FortressMap ChangedSeed,
        SimulationWorld FilledWorld,
        IRuntimeGeologyCatalog Geology);

    private sealed class ThrowingWorldGenStage : IWorldGenStage
    {
        public string Name => "Evidence.ThrowingStage";

        public void Execute(WorldGenContext context)
        {
            throw new InvalidOperationException("intentional WorldGen stage failure");
        }
    }

    private sealed class RecordingWorldGenStage : IWorldGenStage
    {
        public string Name => "Evidence.LaterStage";
        internal bool Executed { get; private set; }

        public void Execute(WorldGenContext context)
        {
            Executed = true;
        }
    }

    private sealed class RecordingDiagnosticSink : IDiagnosticSink
    {
        private readonly List<DiagnosticEvent> _events = new();

        internal IReadOnlyList<DiagnosticEvent> Events => _events;

        public void Write(DiagnosticEvent diagnosticEvent)
        {
            _events.Add(diagnosticEvent);
        }
    }
}
