using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Navigation;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Replay;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.Topology;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using NavigationChunkKey = HumanFortress.Contracts.Navigation.ChunkKey;
using SimulationChunkKey = HumanFortress.Simulation.World.ChunkKey;

internal static class TopologyTransactionRegressionTests
{
    internal static void RunAll()
    {
        TestCrossChunkPlacementAndRemovalCommitOncePerChunk();
        TestCrossChunkValidationFailureHasZeroMutation();
        TestTerrainCommitPublishesBoundaryDependenciesOnce();
        TestBlockerDoorAndIntermediateRouteInvalidation();
        TestTopologyReplayHashIsStable();
    }

    private static void TestCrossChunkPlacementAndRemovalCommitOncePerChunk()
    {
        var world = CreateCrossChunkWorld();
        var primary = world.GetChunk(new SimulationChunkKey(0, 0, 0))!;
        var secondary = world.GetChunk(new SimulationChunkKey(1, 0, 0))!;
        var primaryVersion = primary.ConnectivityVersion;
        var secondaryVersion = secondary.ConnectivityVersion;
        var placeable = CreateCrossChunkPlaceable(
            Guid.Parse("b0000000-0000-0000-0000-000000000001"),
            PassabilityMode.Blocking);

        var placed = PlaceableManager.TryPlacePlaceable(
            world,
            placeable,
            tick: 10,
            out _,
            out var placementChange);
        var dirtyAfterPlacement = world.GetAndClearDirtyChunks();
        var primaryAnchor = Chunk.LocalIndex(31, 4);
        var secondaryCell = Chunk.LocalIndex(0, 4);

        RegressionAssert.True(
            placed
            && placementChange?.AffectedChunks.Count == 2
            && dirtyAfterPlacement.SequenceEqual(new[]
            {
                new SimulationChunkKey(0, 0, 0),
                new SimulationChunkKey(1, 0, 0)
            })
            && primary.ConnectivityVersion == primaryVersion + 1
            && secondary.ConnectivityVersion == secondaryVersion + 1
            && primary.GetPlaceableData()?.TryGetOwnedAt(primaryAnchor, out var owner) == true
            && ReferenceEquals(owner, placeable)
            && secondary.GetPlaceableData()?.TryGetExternalRefAt(secondaryCell, out var ownerGuid) == true
            && ownerGuid == placeable.Guid
            && primary.IsFurnitureBlocked(primaryAnchor)
            && secondary.IsFurnitureBlocked(secondaryCell),
            "Cross-chunk placement did not atomically publish owner/ref/blocker state once per affected chunk.");

        primaryVersion = primary.ConnectivityVersion;
        secondaryVersion = secondary.ConnectivityVersion;
        var removed = PlaceableManager.RemoveOwnedAt(world, placeable.Position, placeable.Z, tick: 11);
        var dirtyAfterRemoval = world.GetAndClearDirtyChunks();

        RegressionAssert.True(
            removed
            && dirtyAfterRemoval.SequenceEqual(new[]
            {
                new SimulationChunkKey(0, 0, 0),
                new SimulationChunkKey(1, 0, 0)
            })
            && primary.ConnectivityVersion == primaryVersion + 1
            && secondary.ConnectivityVersion == secondaryVersion + 1
            && primary.GetPlaceableData()?.HasPlaceableAt(primaryAnchor) == false
            && secondary.GetPlaceableData()?.HasPlaceableAt(secondaryCell) == false
            && !primary.HasFurnitureAt(primaryAnchor)
            && !secondary.HasFurnitureAt(secondaryCell),
            "Cross-chunk removal left stale owner/ref/derived occupancy or published duplicate versions.");

        Console.WriteLine("[PASS] Cross-chunk topology commits are atomic and versioned once");
    }

    private static void TestCrossChunkValidationFailureHasZeroMutation()
    {
        var world = new World(2, 1);
        world.SetTile(31, 4, 0, OpenFloor, tick: 0);
        var primary = world.GetChunk(new SimulationChunkKey(0, 0, 0))!;
        var version = primary.ConnectivityVersion;
        var placeable = CreateCrossChunkPlaceable(
            Guid.Parse("b0000000-0000-0000-0000-000000000002"),
            PassabilityMode.Blocking);

        var placed = PlaceableManager.TryPlacePlaceable(
            world,
            placeable,
            tick: 20,
            out var failure,
            out var committedChange);

        RegressionAssert.True(
            !placed
            && failure.Contains("Chunk not loaded", StringComparison.Ordinal)
            && committedChange == null
            && primary.ConnectivityVersion == version
            && primary.GetPlaceableData() == null
            && !primary.HasFurnitureAt(Chunk.LocalIndex(31, 4))
            && world.GetAndClearDirtyChunks().Count == 0,
            "Missing secondary chunk validation mutated primary placeable/topology state.");

        Console.WriteLine("[PASS] Cross-chunk topology validation fails with zero mutation");
    }

    private static void TestBlockerDoorAndIntermediateRouteInvalidation()
    {
        var world = new World(3, 1);
        for (var x = 0; x < world.SizeInTiles; x++)
            world.SetTile(x, 1, 0, OpenFloor, tick: 0);

        var source = new SimulationNavigationSource(world);
        var tuning = new NavigationTuning
        {
            AllowDiagonals = false,
            MaxNodesPerSearch = 20_000,
            MaxPathsPerTick = 8
        };
        var navigation = new NavigationManager(source, tuning);
        navigation.RebuildAll();
        var view = new WorldNavigationView(navigation);
        var paths = new PathService(tuning);
        var registry = new RuntimePathServiceRegistry();
        registry.Register(paths);
        var request = new PathRequest(
            new Point3(1, 1, 0),
            new Point3(94, 1, 0),
            MoveMode.Walk,
            PathFlags.None,
            Seed: 77);

        paths.BeginTick();
        var initialPath = paths.Solve(in request, view);
        var initialStats = paths.GetStats();
        var door = new PlaceableInstance(
            Guid.Parse("b0000000-0000-0000-0000-000000000003"),
            PlaceableKind.Construction,
            "topology_test_door",
            new Point(40, 1),
            0,
            new Footprint(1, 1, 1),
            new DoorState(IsOpen: false, IsLocked: false))
        {
            Passability = PassabilityMode.Doorway
        };

        PlaceableManager.PlacePlaceable(world, door, tick: 30);
        RebuildAndInvalidateCommittedDirtySet(world, navigation, registry);
        var closedStats = paths.GetStats();
        paths.BeginTick();
        var closedPath = paths.Solve(in request, view);
        var closedWalkable = view.IsWalkable(new Point3(40, 1, 0), MoveMode.Walk);

        var opened = PlaceableManager.TrySetDoorState(
            world,
            door.Guid,
            isOpen: true,
            isLocked: false,
            tick: 31,
            out _);
        RebuildAndInvalidateCommittedDirtySet(world, navigation, registry);
        paths.BeginTick();
        var reopenedPath = paths.Solve(in request, view);

        RegressionAssert.True(
            initialPath.ReachesDestination(request.Destination)
            && initialStats.CacheSize == 1
            && closedStats.CacheSize == 0
            && !closedWalkable
            && closedPath.Kind == PathResultKind.NoPath
            && opened
            && view.IsWalkable(new Point3(40, 1, 0), MoveMode.Walk)
            && reopenedPath.ReachesDestination(request.Destination),
            "Blocker/door topology did not rebuild pathability or invalidate a cached route through an intermediate chunk.");

        Console.WriteLine("[PASS] Blocker and door commits invalidate intermediate cached routes");
    }

    private static void TestTerrainCommitPublishesBoundaryDependenciesOnce()
    {
        var world = CreateCrossChunkWorld();
        var primary = world.GetChunk(new SimulationChunkKey(0, 0, 0))!;
        var neighbor = world.GetChunk(new SimulationChunkKey(1, 0, 0))!;
        var primaryVersion = primary.ConnectivityVersion;
        var neighborVersion = neighbor.ConnectivityVersion;
        var solid = new TileBase(0, (ushort)TerrainKind.SolidWall, 0, 0, 0, 0, 1);

        var change = TopologyChangeTransaction.ApplyTerrain(
            world,
            primary.Key,
            localX: 31,
            localY: 4,
            newTile: solid,
            tick: 25);
        var dirty = world.GetAndClearDirtyChunks();

        RegressionAssert.True(
            change.AffectedChunks.Count == 2
            && dirty.SequenceEqual(new[]
            {
                new SimulationChunkKey(0, 0, 0),
                new SimulationChunkKey(1, 0, 0)
            })
            && primary.ConnectivityVersion == primaryVersion + 1
            && neighbor.ConnectivityVersion == neighborVersion + 1
            && primary.GetTile(31, 4).Kind == TerrainKind.SolidWall,
            "Boundary terrain topology did not publish one shared dependency dirty set.");

        Console.WriteLine("[PASS] Terrain topology publishes boundary dependencies once");
    }

    private static void TestTopologyReplayHashIsStable()
    {
        var worldA = CreateCrossChunkWorld();
        var worldB = CreateCrossChunkWorld();
        var guid = Guid.Parse("b0000000-0000-0000-0000-000000000004");
        var placeableA = CreateCrossChunkPlaceable(guid, PassabilityMode.Doorway);
        var placeableB = CreateCrossChunkPlaceable(guid, PassabilityMode.Doorway);
        PlaceableManager.PlacePlaceable(worldA, placeableA, tick: 40);
        PlaceableManager.PlacePlaceable(worldB, placeableB, tick: 400);
        var closedHashA = WorldReplayHashBuilder.Build(worldA);
        var closedHashB = WorldReplayHashBuilder.Build(worldB);
        PlaceableManager.TrySetDoorState(worldA, guid, true, false, tick: 41, out _);
        PlaceableManager.TrySetDoorState(worldB, guid, true, false, tick: 401, out _);
        var openHashA = WorldReplayHashBuilder.Build(worldA);
        var openHashB = WorldReplayHashBuilder.Build(worldB);

        RegressionAssert.True(
            closedHashA == closedHashB
            && openHashA == openHashB
            && closedHashA != openHashA,
            "Equivalent topology transactions produced unstable replay hashes or omitted door state.");

        Console.WriteLine("[PASS] Topology replay hashes are stable");
    }

    private static void RebuildAndInvalidateCommittedDirtySet(
        World world,
        NavigationManager navigation,
        RuntimePathServiceRegistry registry)
    {
        foreach (var chunk in world.GetAndClearDirtyChunks())
        {
            var key = new NavigationChunkKey(chunk.ChunkX, chunk.ChunkY, chunk.Z);
            navigation.RebuildChunkNavData(key);
            registry.InvalidateChunk(key);
        }
    }

    private static World CreateCrossChunkWorld()
    {
        var world = new World(2, 1);
        world.SetTile(31, 4, 0, OpenFloor, tick: 0);
        world.SetTile(32, 4, 0, OpenFloor, tick: 0);
        return world;
    }

    private static PlaceableInstance CreateCrossChunkPlaceable(Guid guid, PassabilityMode passability)
    {
        return new PlaceableInstance(
            guid,
            PlaceableKind.Construction,
            "cross_chunk_topology_test",
            new Point(31, 4),
            0,
            new Footprint(2, 1, 1),
            passability == PassabilityMode.Doorway ? new DoorState() : null)
        {
            Passability = passability
        };
    }

    private static TileBase OpenFloor =>
        new(0, (ushort)TerrainKind.OpenWithFloor, 0, 0, 0, 0, 1);
}
