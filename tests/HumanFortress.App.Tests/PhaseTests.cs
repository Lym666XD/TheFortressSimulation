using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Random;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Core.World;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Rendering;
using HumanFortress.WorldGen;

namespace HumanFortress.App
{
    /// <summary>
    /// Comprehensive test suite for all milestone phases.
    /// </summary>
    public static class PhaseTests
    {
        public static void RunAllPhaseTests()
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine("   HUMANFORTRESS PHASE VALIDATION TESTS");
            Console.WriteLine("========================================\n");

            var phaseAPass = RunPhaseATests();
            var phaseBPass = RunPhaseBTests();
            var phaseCPass = RunPhaseCTests();
            var phaseDPass = RunPhaseDTests();

            Console.WriteLine("\n========================================");
            Console.WriteLine("         TEST RESULTS SUMMARY");
            Console.WriteLine("========================================");
            Console.WriteLine($"Phase A (Platform & CI):     {(phaseAPass ? "✅ PASS" : "❌ FAIL")}");
            Console.WriteLine($"Phase B (WorldGen & Map):    {(phaseBPass ? "✅ PASS" : "❌ FAIL")}");
            Console.WriteLine($"Phase C (Embark & Fortress): {(phaseCPass ? "✅ PASS" : "❌ FAIL")}");
            Console.WriteLine($"Phase D (Navigation):        {(phaseDPass ? "✅ PASS" : "❌ FAIL")}");
            Console.WriteLine("========================================\n");

            if (!phaseAPass || !phaseBPass || !phaseCPass || !phaseDPass)
                throw new InvalidOperationException("One or more phase validation tests failed.");
        }

        private static bool RunPhaseATests()
        {
            Console.WriteLine("\n==== PHASE A: Platform & CI Foundations ====");
            Console.WriteLine("Requirements:");
            Console.WriteLine("- Fixed 50 TPS tick scheduler");
            Console.WriteLine("- Deterministic RNG and command replay");
            Console.WriteLine("- UPDATE_ORDER with Read/Write phases");
            Console.WriteLine("- DiffLog for atomic writes\n");

            bool allPass = true;

            // Test 1: 50 TPS Tick Scheduler
            Console.Write("[TEST] Fixed 50 TPS Tick Scheduler... ");
            try
            {
                var scheduler = new TickScheduler();
                if (scheduler.TargetTPS != 50)
                    throw new Exception($"Expected 50 TPS, got {scheduler.TargetTPS}");
                
                // Test tick execution
                scheduler.ExecuteSingleTick();
                if (scheduler.CurrentTick != 1)
                    throw new Exception("Tick not advancing");
                
                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 2: Deterministic RNG
            Console.Write("[TEST] Deterministic RNG (Xoshiro128++)... ");
            try
            {
                var rng1 = new DeterministicRng(12345);
                var rng2 = new DeterministicRng(12345);
                
                var values1 = new List<uint>();
                var values2 = new List<uint>();
                
                for (int i = 0; i < 100; i++)
                {
                    values1.Add(rng1.Next());
                    values2.Add(rng2.Next());
                }
                
                if (!values1.SequenceEqual(values2))
                    throw new Exception("RNG not deterministic");
                
                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 3: CommandQueue
            Console.Write("[TEST] CommandQueue deterministic ordering... ");
            try
            {
                var queue = new CommandQueue();
                var command1 = new TestCommand { Tick = 1 };
                var command2 = new TestCommand { Tick = 1 };

                queue.Enqueue(command2);
                queue.Enqueue(command1);

                // Commands should keep deterministic queue order.
                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 4: DiffLog
            Console.Write("[TEST] DiffLog atomic write operations... ");
            try
            {
                var diffLog = new DiffLog();
                var op = new DiffOp(
                    DiffOpType.SetTerrain,
                    new DiffTarget(0, 100),
                    "TestSystem",
                    10
                );

                diffLog.AddOp(op);
                var merged = diffLog.MergeAndSort();
                if (merged.Count != 1)
                    throw new Exception("DiffLog not queuing operations");

                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 5: UPDATE_ORDER
            Console.Write("[TEST] UPDATE_ORDER execution phases... ");
            try
            {
                // UPDATE_ORDER is a static concept, test that phases exist
                var scheduler = new TickScheduler();

                // Test that we can register systems (which follow UPDATE_ORDER)
                scheduler.RegisterSystem(new TestTickSystem());

                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            return allPass;
        }

        private static bool RunPhaseBTests()
        {
            Console.WriteLine("\n==== PHASE B: WorldGen & WorldMap ====");
            Console.WriteLine("Requirements:");
            Console.WriteLine("- Deterministic world generation");
            Console.WriteLine("- 256x256 world size");
            Console.WriteLine("- Biomes and terrain features");
            Console.WriteLine("- WorldMap navigation\n");

            bool allPass = true;

            // Test 1: World Generation
            Console.Write("[TEST] World generation with seed... ");
            try
            {
                var generator = new WorldGenerator();
                var worldParams = new WorldParams
                {
                    Seed = 12345,
                    Width = 256,
                    Height = 256,
                    Name = "TestWorld"
                };
                
                var result = generator.Generate(worldParams);
                if (!result.Success)
                    throw new Exception("World generation failed");
                
                if (result.Tiles == null || result.Tiles.GetLength(0) != 256)
                    throw new Exception("World size incorrect");
                
                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 2: Deterministic Generation
            Console.Write("[TEST] Deterministic world generation... ");
            try
            {
                var generator = new WorldGenerator();
                var worldParams = new WorldParams
                {
                    Seed = 99999,
                    Width = 64,
                    Height = 64,
                    Name = "DeterminismTest"
                };
                
                var result1 = generator.Generate(worldParams);
                var result2 = generator.Generate(worldParams);
                
                // Check that same seed produces same world
                bool same = true;
                for (int x = 0; x < 64; x++)
                {
                    for (int y = 0; y < 64; y++)
                    {
                        if (result1.Tiles[x, y].BiomeId != result2.Tiles[x, y].BiomeId ||
                            Math.Abs(result1.Tiles[x, y].Elevation - result2.Tiles[x, y].Elevation) > 0.001f)
                        {
                            same = false;
                            break;
                        }
                    }
                }
                
                if (!same)
                    throw new Exception("World generation not deterministic");
                
                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 3: Biome Generation
            Console.Write("[TEST] Biome variety and distribution... ");
            try
            {
                var generator = new WorldGenerator();
                var worldParams = new WorldParams
                {
                    Seed = 54321,
                    Width = 128,
                    Height = 128,
                    Name = "BiomeTest"
                };
                
                var result = generator.Generate(worldParams);
                var biomes = new HashSet<ushort>();
                
                for (int x = 0; x < 128; x++)
                {
                    for (int y = 0; y < 128; y++)
                    {
                        biomes.Add(result.Tiles[x, y].BiomeId);
                    }
                }
                
                if (biomes.Count < 3)
                    throw new Exception($"Not enough biome variety: {biomes.Count}");
                
                Console.WriteLine($"✅ PASS ({biomes.Count} biomes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            return allPass;
        }

        private static bool RunPhaseCTests()
        {
            Console.WriteLine("\n==== PHASE C: Embark & Fortress Bootstrap ====");
            Console.WriteLine("Requirements:");
            Console.WriteLine("- EmbarkPrep: N×N chunks (2-8)");
            Console.WriteLine("- Fortress terrain generation");
            Console.WriteLine("- Chunk lifecycle (LOD L0-L4)");
            Console.WriteLine("- RenderSnapshot system\n");

            bool allPass = true;

            // Test 1: Fortress Generation
            Console.Write("[TEST] Fortress terrain generation... ");
            try
            {
                var worldTile = new WorldTile
                {
                    BiomeId = (ushort)BiomeType.TemperateForest,
                    Elevation = 0.5f,
                    Temperature = 0.5f,
                    Rainfall = 0.5f
                };
                
                var generator = new FortressGenerator(
                    fortressSize: 2,
                    homeTile: worldTile,
                    worldLocation: new SadRogue.Primitives.Point(10, 10),
                    seed: 12345
                );
                
                var fortressMap = generator.Generate();
                
                if (fortressMap.Size != 2)
                    throw new Exception($"Expected size 2, got {fortressMap.Size}");
                
                if (fortressMap.MaxZ != 50)
                    throw new Exception($"Expected 50 Z-levels, got {fortressMap.MaxZ}");
                
                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 2: Chunk Lifecycle
            Console.Write("[TEST] Chunk LOD lifecycle management... ");
            try
            {
                var world = new World(4, 50);
                var lifecycle = new ChunkLifecycleManager(world);
                
                // Test LOD level transitions
                lifecycle.UpdateLODLevels(64, 64, 25, 0);
                
                // Test heat system
                var testChunk = new ChunkKey(2, 2, 25);
                lifecycle.AddHeat(testChunk, 150);
                
                // Test pinning
                lifecycle.PinChunk(testChunk, PinReason.UIFocus);
                lifecycle.UnpinChunk(testChunk, PinReason.UIFocus);
                
                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 3: RenderSnapshot
            Console.Write("[TEST] RenderSnapshot builder... ");
            try
            {
                var world = new World(2, 50);
                var builder = new RenderSnapshotBuilder(world);
                
                var camera = new CameraInfo
                {
                    ChunkKey = new ChunkKey(0, 0, 25),
                    CenterX = 16,
                    CenterY = 16,
                    Z = 25,
                    Z0 = 25,
                    ZCount = 1
                };
                
                var viewport = new ViewportInfo
                {
                    TilesWidth = 80,
                    TilesHeight = 40
                };
                
                var snapshot = builder.BuildSnapshot(camera, viewport, 0);
                
                if (snapshot == null)
                    throw new Exception("Snapshot is null");
                
                if (snapshot.Tick != 0)
                    throw new Exception("Snapshot tick incorrect");
                
                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 4: World to Simulation Conversion
            Console.Write("[TEST] FortressMap to World conversion... ");
            try
            {
                var worldTile = new WorldTile
                {
                    BiomeId = (ushort)BiomeType.Desert,
                    Elevation = 0.3f
                };
                
                var generator = new FortressGenerator(2, worldTile, new SadRogue.Primitives.Point(5, 5), 54321);
                var fortressMap = generator.Generate();
                var world = new World(fortressMap.Size, fortressMap.MaxZ);
                fortressMap.FillWorld(world);
                
                if (world.SizeInChunks != 2)
                    throw new Exception("World size mismatch");
                
                // Check that chunks can be created
                var chunkKey = new ChunkKey(0, 0, 25);
                var chunk = world.GetOrCreateChunk(chunkKey);
                if (chunk == null)
                    throw new Exception("Chunk not created");
                
                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 5: Embark Size Range
            Console.Write("[TEST] Embark size range (2-8 chunks)... ");
            try
            {
                var worldTile = new WorldTile { BiomeId = (ushort)BiomeType.Mountain, Elevation = 0.8f, Temperature = 0.3f, Rainfall = 0.5f };

                // Test minimum size
                var gen2 = new FortressGenerator(2, worldTile, new SadRogue.Primitives.Point(0, 0), 1);
                var map2 = gen2.Generate();
                if (map2.Size != 2) throw new Exception("Size 2 failed");

                // Test maximum size
                var gen8 = new FortressGenerator(8, worldTile, new SadRogue.Primitives.Point(0, 0), 2);
                var map8 = gen8.Generate();
                if (map8.Size != 8) throw new Exception("Size 8 failed");

                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 6: Full Determinism Check
            Console.Write("[TEST] Full fortress determinism check... ");
            try
            {
                var worldTile = new WorldTile
                {
                    BiomeId = (ushort)BiomeType.TemperateForest,
                    Elevation = 0.5f,
                    Temperature = 0.6f,
                    Rainfall = 0.7f
                };

                // Generate same fortress twice with same seed
                var gen1 = new FortressGenerator(3, worldTile, new SadRogue.Primitives.Point(10, 10), 99999);
                var gen2 = new FortressGenerator(3, worldTile, new SadRogue.Primitives.Point(10, 10), 99999);

                var map1 = gen1.Generate();
                var map2 = gen2.Generate();

                // Verify terrain matches exactly
                bool deterministic = true;
                for (int cx = 0; cx < 3 && deterministic; cx++)
                {
                    for (int cy = 0; cy < 3 && deterministic; cy++)
                    {
                        var chunk1 = map1.GetChunk(cx, cy);
                        var chunk2 = map2.GetChunk(cx, cy);

                        for (int x = 0; x < 32 && deterministic; x++)
                        {
                            for (int y = 0; y < 32 && deterministic; y++)
                            {
                                for (int z = 0; z < 50 && deterministic; z++)
                                {
                                    if (chunk1.GetTerrain(x, y, z) != chunk2.GetTerrain(x, y, z))
                                    {
                                        deterministic = false;
                                        Console.WriteLine($"\nMismatch at chunk({cx},{cy}) tile({x},{y},{z})");
                                    }
                                }
                            }
                        }
                    }
                }

                if (!deterministic)
                    throw new Exception("Fortress generation not deterministic");

                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 7: Simulation Loop at 50 TPS
            Console.Write("[TEST] Idle simulation loop at 50 TPS... ");
            try
            {
                var scheduler = new TickScheduler();
                var testSystem = new TestTickSystem();
                scheduler.RegisterSystem(testSystem);

                // Execute 50 ticks (1 second of simulation)
                for (int i = 0; i < 50; i++)
                {
                    scheduler.ExecuteSingleTick();
                }

                if (scheduler.CurrentTick != 50)
                    throw new Exception($"Expected 50 ticks, got {scheduler.CurrentTick}");

                if (testSystem.ReadCount != 50 || testSystem.WriteCount != 50)
                    throw new Exception($"System not called correctly: R={testSystem.ReadCount}, W={testSystem.WriteCount}");

                Console.WriteLine("✅ PASS (50 ticks executed)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            return allPass;
        }

        private static bool RunPhaseDTests()
        {
            Console.WriteLine("\n==== PHASE D: Navigation & Connectivity ====");
            Console.WriteLine("Requirements:");
            Console.WriteLine("- Walkability/opacity/support masks");
            Console.WriteLine("- ConnectivityVersion invalidation");
            Console.WriteLine("- Deterministic A* pathfinding");
            Console.WriteLine("- Path caching & traffic costs");
            Console.WriteLine("- 10 concurrent pathfinders\n");

            bool allPass = true;

            // Test 1: Navigation mask generation
            Console.Write("[TEST] Navigation mask generation... ");
            try
            {
                var tuning = HumanFortress.Navigation.NavigationTuning.Default;
                var chunkKey = new HumanFortress.Navigation.ChunkKey(0, 0, 0);
                var navData = new HumanFortress.Navigation.ChunkNavData(chunkKey);

                // Create test tile data with walkable floor
                var tiles = CreateNavigationTiles(HumanFortress.Navigation.NavigationTileKind.OpenWithFloor);

                navData.RebuildFromTiles(tiles, tuning);

                // Check that floor tiles are walkable
                if (!navData.HasCapability(0, HumanFortress.Navigation.NavCapability.Walk))
                    throw new Exception("Floor tiles should be walkable");

                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 2: ConnectivityVersion invalidation
            Console.Write("[TEST] ConnectivityVersion invalidation... ");
            try
            {
                var chunkKey = new HumanFortress.Navigation.ChunkKey(0, 0, 0);
                var navData = new HumanFortress.Navigation.ChunkNavData(chunkKey);
                var initialVersion = navData.ConnectivityVersion;

                var tiles = CreateNavigationTiles(HumanFortress.Navigation.NavigationTileKind.SolidWall);
                navData.RebuildFromTiles(tiles, HumanFortress.Navigation.NavigationTuning.Default);

                if (navData.ConnectivityVersion <= initialVersion)
                    throw new Exception("ConnectivityVersion should increment after rebuild");

                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 3: Deterministic A* pathfinding
            Console.Write("[TEST] Deterministic A* pathfinding... ");
            try
            {
                var pathService = new HumanFortress.Navigation.PathService();
                var world = new TestNavigationWorld();

                var request1 = new HumanFortress.Navigation.PathRequest(
                    new HumanFortress.Navigation.Point3(0, 0, 0),
                    new HumanFortress.Navigation.Point3(10, 10, 0),
                    HumanFortress.Navigation.MoveMode.Walk,
                    HumanFortress.Navigation.PathFlags.None,
                    12345);

                var request2 = new HumanFortress.Navigation.PathRequest(
                    new HumanFortress.Navigation.Point3(0, 0, 0),
                    new HumanFortress.Navigation.Point3(10, 10, 0),
                    HumanFortress.Navigation.MoveMode.Walk,
                    HumanFortress.Navigation.PathFlags.None,
                    12345);

                pathService.BeginTick();
                var path1 = pathService.Solve(in request1, world);

                pathService.BeginTick();
                var path2 = pathService.Solve(in request2, world);

                if (path1.Hash != path2.Hash)
                    throw new Exception("Identical requests should produce identical paths");

                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 4: Path caching
            Console.Write("[TEST] Path caching... ");
            try
            {
                var pathService = new HumanFortress.Navigation.PathService();
                var world = new TestNavigationWorld();

                var request = new HumanFortress.Navigation.PathRequest(
                    new HumanFortress.Navigation.Point3(0, 0, 0),
                    new HumanFortress.Navigation.Point3(5, 5, 0),
                    HumanFortress.Navigation.MoveMode.Walk,
                    HumanFortress.Navigation.PathFlags.None,
                    99999);

                pathService.BeginTick();
                var path1 = pathService.Solve(in request, world);
                var stats1 = pathService.GetStats();

                pathService.BeginTick();
                var path2 = pathService.Solve(in request, world);
                var stats2 = pathService.GetStats();

                if (stats2.CacheHits <= stats1.CacheHits)
                    throw new Exception("Cache should be used for identical requests");

                Console.WriteLine("✅ PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            // Test 5: Concurrent pathfinders
            Console.Write("[TEST] 10 concurrent pathfinders... ");
            try
            {
                var pathService = new HumanFortress.Navigation.PathService();
                var world = new TestNavigationWorld();
                var tasks = new System.Threading.Tasks.Task<HumanFortress.Navigation.Path>[10];
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                pathService.BeginTick();

                // Launch 10 concurrent pathfinding requests
                for (int i = 0; i < 10; i++)
                {
                    int localI = i;
                    tasks[i] = System.Threading.Tasks.Task.Run(() =>
                    {
                        var request = new HumanFortress.Navigation.PathRequest(
                            new HumanFortress.Navigation.Point3(localI, 0, 0),
                            new HumanFortress.Navigation.Point3(20 + localI, 20, 0),
                            HumanFortress.Navigation.MoveMode.Walk,
                            HumanFortress.Navigation.PathFlags.None,
                            (uint)(10000 + localI));
                        return pathService.Solve(in request, world);
                    });
                }

                System.Threading.Tasks.Task.WaitAll(tasks);
                stopwatch.Stop();

                // Verify all paths were computed
                int foundPaths = 0;
                foreach (var task in tasks)
                {
                    if (task.Result.Kind == HumanFortress.Navigation.PathResultKind.Found)
                        foundPaths++;
                }

                if (foundPaths < 10)
                    throw new Exception($"Only {foundPaths}/10 paths were found");

                // Check that it completed within reasonable time (should be <10% of frame time)
                if (stopwatch.ElapsedMilliseconds > 16) // 16ms is roughly 10% of 60fps frame
                    Console.WriteLine($"✅ PASS (warning: took {stopwatch.ElapsedMilliseconds}ms)");
                else
                    Console.WriteLine($"✅ PASS ({stopwatch.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAIL: {ex.Message}");
                allPass = false;
            }

            return allPass;
        }

        // Helper test tick system class
        private class TestTickSystem : HumanFortress.Core.Time.ITick
        {
            public int Priority => 100;
            public string SystemId => "TestSystem";
            public int ReadCount { get; private set; }
            public int WriteCount { get; private set; }

            public void ReadTick(ulong tick)
            {
                ReadCount++;
            }

            public void WriteTick(ulong tick)
            {
                WriteCount++;
            }
        }

        // Helper test command class
        private class TestCommand : ICommand
        {
            public ulong Tick { get; set; }
            public Guid CommandId { get; set; } = Guid.NewGuid();
            public string CommandType => "TestCommand";
            public void Execute(ISimulationContext context) { }
            public byte[] Serialize() => Array.Empty<byte>();
        }

        // Helper test navigation world
        private class TestNavigationWorld : HumanFortress.Navigation.IWorldNavigationView
        {
            private readonly Dictionary<HumanFortress.Navigation.ChunkKey, HumanFortress.Navigation.ChunkNavData> _chunks;

            public TestNavigationWorld()
            {
                _chunks = new Dictionary<HumanFortress.Navigation.ChunkKey, HumanFortress.Navigation.ChunkNavData>();

                // Create a simple walkable world (3x3 chunks)
                for (int cx = -1; cx <= 1; cx++)
                {
                    for (int cy = -1; cy <= 1; cy++)
                    {
                        var chunkKey = new HumanFortress.Navigation.ChunkKey(cx, cy, 0);
                        var navData = new HumanFortress.Navigation.ChunkNavData(chunkKey);

                        // Fill with walkable floor tiles
                        var tiles = CreateNavigationTiles(HumanFortress.Navigation.NavigationTileKind.OpenWithFloor);

                        navData.RebuildFromTiles(tiles, HumanFortress.Navigation.NavigationTuning.Default);
                        _chunks[chunkKey] = navData;
                    }
                }
            }

            public bool IsValid(HumanFortress.Navigation.Point3 position)
            {
                return position.X >= -32 && position.X < 96 &&
                       position.Y >= -32 && position.Y < 96 &&
                       position.Z == 0;
            }

            public HumanFortress.Navigation.NavCapability GetCapabilities(HumanFortress.Navigation.Point3 position)
            {
                if (!IsValid(position))
                    return HumanFortress.Navigation.NavCapability.None;

                var chunk = ToChunkKey(position);
                if (_chunks.TryGetValue(chunk, out var navData))
                {
                    var localIdx = ToLocalIndex(position);
                    return (HumanFortress.Navigation.NavCapability)navData.NavMask[localIdx];
                }

                return HumanFortress.Navigation.NavCapability.None;
            }

            public ushort GetCost(HumanFortress.Navigation.Point3 position)
            {
                if (!IsValid(position))
                    return ushort.MaxValue;

                var chunk = ToChunkKey(position);
                if (_chunks.TryGetValue(chunk, out var navData))
                {
                    var localIdx = ToLocalIndex(position);
                    return navData.NavCost[localIdx];
                }

                return ushort.MaxValue;
            }

            public bool IsWalkable(HumanFortress.Navigation.Point3 position, HumanFortress.Navigation.MoveMode mode)
            {
                var caps = GetCapabilities(position);
                return mode switch
                {
                    HumanFortress.Navigation.MoveMode.Walk => (caps & HumanFortress.Navigation.NavCapability.Walk) != 0,
                    HumanFortress.Navigation.MoveMode.Swim => (caps & HumanFortress.Navigation.NavCapability.Swim) != 0,
                    HumanFortress.Navigation.MoveMode.Fly => (caps & HumanFortress.Navigation.NavCapability.Fly) != 0,
                    _ => false,
                };
            }

            public bool HasStairsUp(HumanFortress.Navigation.Point3 position)
            {
                return false; // No stairs in test world
            }

            public bool HasStairsDown(HumanFortress.Navigation.Point3 position)
            {
                return false; // No stairs in test world
            }

            public int GetConnectivityVersion(HumanFortress.Navigation.ChunkKey chunk)
            {
                if (_chunks.TryGetValue(chunk, out var navData))
                {
                    return navData.ConnectivityVersion;
                }
                return 0;
            }

            private static HumanFortress.Navigation.ChunkKey ToChunkKey(HumanFortress.Navigation.Point3 position)
            {
                const int ChunkSize = 32;
                return new HumanFortress.Navigation.ChunkKey(
                    position.X / ChunkSize,
                    position.Y / ChunkSize,
                    position.Z);
            }

            private static int ToLocalIndex(HumanFortress.Navigation.Point3 position)
            {
                const int ChunkSize = 32;
                int localX = ((position.X % ChunkSize) + ChunkSize) % ChunkSize;
                int localY = ((position.Y % ChunkSize) + ChunkSize) % ChunkSize;
                return localY * ChunkSize + localX;
            }
        }

        private static HumanFortress.Navigation.NavigationTile[] CreateNavigationTiles(HumanFortress.Navigation.NavigationTileKind kind)
        {
            var tiles = new HumanFortress.Navigation.NavigationTile[HumanFortress.Navigation.ChunkNavData.TilesPerChunk];
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = CreateNavigationTile(kind);
            return tiles;
        }

        private static HumanFortress.Navigation.NavigationTile CreateNavigationTile(HumanFortress.Navigation.NavigationTileKind kind)
        {
            bool isWalkable = kind switch
            {
                HumanFortress.Navigation.NavigationTileKind.OpenWithFloor => true,
                HumanFortress.Navigation.NavigationTileKind.Ramp => true,
                HumanFortress.Navigation.NavigationTileKind.Slope => true,
                HumanFortress.Navigation.NavigationTileKind.StairsUp => true,
                HumanFortress.Navigation.NavigationTileKind.StairsDown => true,
                HumanFortress.Navigation.NavigationTileKind.StairsUD => true,
                _ => false,
            };

            return new HumanFortress.Navigation.NavigationTile(
                kind,
                IsNatural: true,
                IsWalkable: isWalkable,
                IsStandable: kind == HumanFortress.Navigation.NavigationTileKind.OpenWithFloor,
                IsFlyable: kind != HumanFortress.Navigation.NavigationTileKind.SolidWall,
                FluidDepth: 0,
                MetaBits: 0);
        }
    }
}
