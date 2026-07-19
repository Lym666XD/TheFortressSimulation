using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Commands;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Diagnostics;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    void IFortressRuntimeHeadlessScenarioSessionPorts.FillDeterministicFlatWorld(int standableZ)
    {
        var session = _runtimeSession
            ?? throw new InvalidOperationException("World not initialized");
        var world = session.World;
        if (standableZ < 0 || standableZ >= world.MaxZ)
            throw new ArgumentOutOfRangeException(nameof(standableZ));

        var floor = new TileBase(
            geoMatId: 0,
            terrainBits: (ushort)TerrainKind.OpenWithFloor,
            surfaceBits: 0,
            fluidKind: 0,
            fluidDepth: 0,
            metaBits: 0,
            trafficCost: 1);
        var wall = new TileBase(
            geoMatId: 0,
            terrainBits: (ushort)TerrainKind.SolidWall,
            surfaceBits: 0,
            fluidKind: 0,
            fluidDepth: 0,
            metaBits: 0,
            trafficCost: 1);

        for (var z = 0; z < world.MaxZ; z++)
        {
            var tile = z == standableZ ? floor : wall;
            for (var y = 0; y < world.SizeInTiles; y++)
            {
                for (var x = 0; x < world.SizeInTiles; x++)
                    world.SetTile(x, y, z, tile, tick: 0);
            }
        }

        session.Navigation.RebuildAll();
        InvalidateFrameSnapshots();
    }

    RuntimeHeadlessWorkloadResult IFortressRuntimeHeadlessScenarioSessionPorts.SeedWorkload(
        RuntimeHeadlessWorkloadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ItemDefinitionId))
            throw new ArgumentException("Item definition id must not be blank.", nameof(request));
        if (request.ItemInstanceCount < 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.TransportRequestCount < 0
            || request.TransportRequestCount > request.ItemInstanceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Transport request count must be between zero and the item instance count.");
        }

        var session = _runtimeSession
            ?? throw new InvalidOperationException("World not initialized");
        var systems = session.Host.RequireSystems();
        var world = session.World;
        if (request.Z < 0 || request.Z >= world.MaxZ)
            throw new ArgumentOutOfRangeException(nameof(request));
        if (world.SizeInTiles < 4)
            throw new InvalidOperationException("Headless workload requires a world at least four tiles wide.");

        var itemsBefore = world.Items.InstanceCount;
        var pendingBefore = systems.TransportQueue.Count;
        var spawned = new List<(Guid Id, Point Source)>(request.ItemInstanceCount);
        var innerSize = world.SizeInTiles - 2;
        for (var index = 0; index < request.ItemInstanceCount; index++)
        {
            var source = new Point(
                1 + index % innerSize,
                1 + index / innerSize % innerSize);
            var itemId = world.Items.SpawnItem(
                request.ItemDefinitionId,
                source,
                request.Z,
                quantity: 1,
                currentTick: 0);
            if (!itemId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Headless workload failed to spawn item {index} of {request.ItemInstanceCount} "
                    + $"for definition '{request.ItemDefinitionId}'.");
            }

            spawned.Add((itemId.Value, source));
        }

        for (var index = 0; index < request.TransportRequestCount; index++)
        {
            var item = spawned[index];
            var destination = new Point(
                world.SizeInTiles - 1 - item.Source.X,
                world.SizeInTiles - 1 - item.Source.Y);
            if (!systems.TransportQueue.Enqueue(new TransportRequest(
                    item.Id,
                    item.Source,
                    request.Z,
                    destination,
                    request.Z,
                    Quantity: 1,
                    TransportReason.Misc,
                    Priority: index % 4,
                    RequestorId: $"stage6:{index:D8}",
                    CreatedTick: 0,
                    Seed: unchecked((uint)(index + 1)))))
            {
                throw new InvalidOperationException(
                    $"Headless workload failed to enqueue transport request {index}.");
            }
        }

        var itemsSeeded = world.Items.InstanceCount - itemsBefore;
        var requestsSeeded = systems.TransportQueue.Count - pendingBefore;
        if (itemsSeeded != request.ItemInstanceCount
            || requestsSeeded != request.TransportRequestCount)
        {
            throw new InvalidOperationException(
                "Headless workload did not reach its declared authority counts: "
                + $"items={itemsSeeded}/{request.ItemInstanceCount}, "
                + $"transport={requestsSeeded}/{request.TransportRequestCount}.");
        }

        return new RuntimeHeadlessWorkloadResult(itemsSeeded, requestsSeeded);
    }

    RuntimeHeadlessCachePrimeResult IFortressRuntimeHeadlessScenarioSessionPorts.PrimeDerivedCaches()
    {
        var session = _runtimeSession
            ?? throw new InvalidOperationException("World not initialized");
        var systems = session.Host.RequireSystems();
        var pathServices = session.Host.PathServices
            ?? throw new InvalidOperationException("Runtime path-service registry is unavailable.");
        var pathQuery = new RuntimeNavigationServices(pathServices)
            .CreatePathQueryServices(session.Navigation, systems.TransportJobs.PathService);

        var creatures = session.World.Creatures.GetAllInstances().ToArray();
        if (creatures.Length == 0)
            throw new InvalidOperationException("Cannot prime path caches without an initial creature.");

        var creature = creatures[0];
        var source = new Point3(creature.Position.X, creature.Position.Y, creature.Z);
        var requests = new[]
        {
            new PathRequest(source, source, MoveMode.Walk, PathFlags.None, 1),
            new PathRequest(source, source, MoveMode.Walk, PathFlags.None, 2),
            new PathRequest(source, source, MoveMode.Walk, PathFlags.None, 3),
            new PathRequest(source, source, MoveMode.Walk, PathFlags.None, 4),
        };
        var before = pathServices.CaptureMetrics();
        var complete = 0;
        pathQuery.PathService.BeginTick();
        foreach (var request in requests)
        {
            var worldView = pathQuery.WorldView;
            if (pathQuery.PathService.Solve(in request, in worldView).ReachesDestination(request.Destination))
                complete++;
        }

        pathQuery.PathService.BeginTick();
        foreach (var request in requests)
        {
            var worldView = pathQuery.WorldView;
            if (pathQuery.PathService.Solve(in request, in worldView).ReachesDestination(request.Destination))
                complete++;
        }
        pathQuery.PathService.BeginTick();

        var after = pathServices.CaptureMetrics();
        var addedHits = after.CacheHitsTotal - before.CacheHitsTotal;
        if (complete != requests.Length * 2 || addedHits < requests.Length)
        {
            throw new InvalidOperationException(
                $"Derived-cache prime was incomplete: paths={complete}/{requests.Length * 2}, "
                + $"cacheHitsAdded={addedHits}.");
        }

        return new RuntimeHeadlessCachePrimeResult(
            RequestsIssued: requests.Length * 2,
            CompletePaths: complete,
            CacheHitsAdded: addedHits);
    }

    RuntimeCommandReplayRestoreResult IFortressRuntimeHeadlessScenarioSessionPorts.RestoreCommandJournal(
        IReadOnlyList<CommandReplayRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return new RuntimeCommandReplayRestorer().RestorePending(_services, records);
    }
}
