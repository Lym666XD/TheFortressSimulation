using System.Collections.Immutable;
using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Diff;
using HumanFortress.Jobs.Transport.Planning;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Stockpile;

namespace HumanFortress.Jobs.Transport;

internal sealed partial class TransportJobExecutor
{
    internal void ReadPlan(ulong tick, int workerCount = 1)
    {
        _paths.BeginTick();
        var snapshot = CapturePlanningSnapshot(tick);
        var resolution = TransportPlanningPipeline
            .PlanAsync(snapshot, workerCount)
            .GetAwaiter()
            .GetResult();

        var movement = new Dictionary<Guid, MovementProposalData>();
        foreach (var intent in resolution.Accepted)
        {
            if (intent.Kind != TransportIntentKind.AdvanceMovement)
                continue;

            ulong entityKey = DiffTargetEncoding.EntityKey(intent.CreatureId);
            movement[intent.CreatureId] = _move.PlanMovement(entityKey, _navView);
        }

        _preparedTick = new PreparedTransportTick(
            tick,
            snapshot,
            resolution,
            movement.ToImmutableDictionary());
    }

    private TransportPlanningSnapshot CapturePlanningSnapshot(ulong tick)
    {
        var queuedRequests = _requests.GetStateSnapshot().PendingRequests;
        var backlogState = _backlog.GetStateSnapshot();
        var creatureInstances = _world.Creatures.GetAllInstances().ToArray();
        var queue = queuedRequests
            .Select((request, index) => new TransportQueuedRequestReadRow(
                index,
                ToReadRow(
                    request,
                    index,
                    _assignmentHandler.CaptureRankedHaulingWorkerIds(
                        creatureInstances,
                        tick,
                        new Point3(request.From.X, request.From.Y, request.FromZ)))))
            .ToArray();
        var backlog = backlogState
            .Select(entry => new TransportBacklogRequestReadRow(
                entry.Order,
                entry.EnqueuedTick,
                ToReadRow(
                    entry.Request,
                    entry.Order,
                    _assignmentHandler.CaptureRankedHaulingWorkerIds(
                        creatureInstances,
                        tick,
                        new Point3(
                            entry.Request.From.X,
                            entry.Request.From.Y,
                            entry.Request.FromZ)))))
            .ToArray();

        var items = _world.Items.GetAllInstances()
            .Select(static item => new TransportItemReadRow(
                item.Guid,
                DiffTargetEncoding.EntityKey(item.Guid),
                item.DefinitionId,
                new Point3(item.Position.X, item.Position.Y, item.Z),
                item.StackCount,
                item.IsOnGround,
                item.CarriedBy ?? Guid.Empty))
            .ToArray();
        var haulingEligible = queue
            .SelectMany(static row => row.Request.RankedWorkerIds)
            .Concat(backlog.SelectMany(static row => row.Request.RankedWorkerIds))
            .ToHashSet();
        if (queue.Length == 0 && backlog.Length == 0)
        {
            haulingEligible = _assignmentHandler.CaptureRankedHaulingWorkerIds(
                    creatureInstances,
                    tick,
                    Point3.Zero)
                .ToHashSet();
        }
        var creatures = creatureInstances
            .Select(creature => new TransportCreatureReadRow(
                creature.Guid,
                DiffTargetEncoding.EntityKey(creature.Guid),
                new Point3(creature.Position.X, creature.Position.Y, creature.Z),
                creature.HP,
                CanHaul: haulingEligible.Contains(creature.Guid)))
            .ToArray();

        var active = new TransportActiveJobReadRow[_active.Count];
        var cursors = new List<TransportMovementCursorReadRow>(creatures.Length);
        var capturedCursorCreatures = new HashSet<Guid>();
        var revisionChunks = new HashSet<ChunkKey>();
        for (int index = 0; index < _active.Count; index++)
        {
            var job = _active[index];
            active[index] = new TransportActiveJobReadRow(
                job.ItemId,
                job.CreatureId,
                job.Dest,
                job.Stage,
                job.Quantity,
                job.Reason,
                Priority: 0,
                CreatedTick: tick,
                SystemOrder: JobDiffSystemOrder.Transport,
                ProducerId: SystemId,
                LocalSequence: (ulong)index,
                job.InvalidReplanCount,
                job.PathSearchAttempt,
                job.PendingSplitReservation.ResourceId,
                job.PendingSplitReservation.Generation,
                job.PendingSplitIssuedTick);

            revisionChunks.Add(ToNavigationChunk(job.Dest));

            var cursor = CaptureMovementCursor(job.CreatureId);
            if (!cursor.HasValue)
                continue;

            var value = cursor.Value;
            revisionChunks.Add(value.CurrentChunk);
            revisionChunks.Add(ToNavigationChunk(value.Destination));
            cursors.Add(value);
            capturedCursorCreatures.Add(job.CreatureId);
        }

        foreach (var creature in creatures)
        {
            if (capturedCursorCreatures.Contains(creature.CreatureId))
                continue;

            var cursor = CaptureMovementCursor(creature.CreatureId);
            if (!cursor.HasValue)
                continue;

            var value = cursor.Value;
            revisionChunks.Add(value.CurrentChunk);
            revisionChunks.Add(ToNavigationChunk(value.Destination));
            cursors.Add(value);
        }

        foreach (var request in queue.Select(static row => row.Request)
                     .Concat(backlog.Select(static row => row.Request)))
        {
            revisionChunks.Add(ToNavigationChunk(request.Source));
            revisionChunks.Add(ToNavigationChunk(request.Destination));
        }

        foreach (var creature in creatures)
            revisionChunks.Add(ToNavigationChunk(creature.Position));
        foreach (var item in items)
            revisionChunks.Add(ToNavigationChunk(item.Position));

        var reservations = _world.Reservations.GetItemReservationsSnapshot()
            .Select(static entry => new TransportReservationReadRow(
                TransportReservationResourceKind.Item,
                entry.itemId,
                entry.holderId,
                entry.systemId,
                entry.jobId,
                entry.generation,
                entry.expireTick,
                entry.stagedTransfer))
            .Concat(_world.Reservations.GetCreatureReservationsSnapshot()
                .Select(static entry => new TransportReservationReadRow(
                    TransportReservationResourceKind.Creature,
                    entry.workerId,
                    Guid.Empty,
                    entry.holderSystem,
                    entry.jobId,
                    entry.generation,
                    entry.expireTick,
                    IsStagedTransfer: false)))
            .ToArray();

        var stockpileObservations = CaptureStockpileObservations(queue, backlog, active, items);
        var revisions = revisionChunks
            .Select(chunk => new TransportNavigationRevisionReadRow(
                chunk,
                unchecked((ulong)_navView.GetConnectivityVersion(chunk))))
            .ToArray();

        return TransportPlanningSnapshot.Create(
            tick,
            queue,
            backlog,
            active,
            items,
            creatures,
            reservations,
            stockpileObservations.Cells,
            revisions,
            cursors,
            stockpileObservations.NonStockpileDestinations);
    }

    private StockpilePlanningObservations CaptureStockpileObservations(
        IReadOnlyList<TransportQueuedRequestReadRow> queue,
        IReadOnlyList<TransportBacklogRequestReadRow> backlog,
        IReadOnlyList<TransportActiveJobReadRow> active,
        IReadOnlyList<TransportItemReadRow> items)
    {
        var itemById = items.ToDictionary(static item => item.ItemId);
        var rows = new Dictionary<Point3, TransportStockpileCellReadRow>();
        var observed = new HashSet<Point3>();
        var nonStockpileDestinations = new HashSet<Point3>();
        var destinations = queue
            .Select(static row => (
                ItemId: row.Request.ItemId,
                Destination: row.Request.Destination,
                row.Request.Reason))
            .Concat(backlog.Select(static row => (
                ItemId: row.Request.ItemId,
                Destination: row.Request.Destination,
                row.Request.Reason)))
            .Concat(active.Select(static row => (
                row.ItemId,
                Destination: row.Destination,
                row.Reason)))
            .Where(static candidate =>
                TransportDestinationValidator.WritesStockpileIndex(candidate.Reason));
        foreach (var request in destinations)
        {
            if (!observed.Add(request.Destination))
                continue;

            if (!StockpileWorldQueries.TryGetStockpileCell(
                    _world,
                    request.Destination.X,
                    request.Destination.Y,
                    request.Destination.Z,
                    out var location))
            {
                nonStockpileDestinations.Add(request.Destination);
                continue;
            }

            var zone = _world.Stockpiles.GetZone(location.ZoneId);
            if (zone == null)
                continue;

            string acceptedId = itemById.TryGetValue(request.ItemId, out var item)
                && _world.Items.GetInstance(request.ItemId) is { } instance
                && zone.Filter.Accepts(StockpileItemProjection.FromItem(
                    instance,
                    _world.Items.GetDefinition(item.DefinitionId)))
                    ? item.DefinitionId
                    : "__rejected__";
            var occupying = _world.Items
                .GetGroundItemsAt(
                    new SadRogue.Primitives.Point(request.Destination.X, request.Destination.Y),
                    request.Destination.Z)
                .Select(static value => value.Guid)
                .FirstOrDefault();
            rows.Add(request.Destination, new TransportStockpileCellReadRow(
                location.ZoneId,
                request.Destination,
                zone.Generation,
                ImmutableArray.Create(acceptedId),
                occupying));
        }

        return new StockpilePlanningObservations(
            rows.Values.ToArray(),
            nonStockpileDestinations.ToArray());
    }

    private static TransportRequestReadRow ToReadRow(
        TransportRequest request,
        int sequence,
        IReadOnlyList<Guid> rankedWorkerIds)
    {
        return new TransportRequestReadRow(
            request.ItemGuid,
            new Point3(request.From.X, request.From.Y, request.FromZ),
            new Point3(request.To.X, request.To.Y, request.ToZ),
            request.Quantity,
            request.Reason,
            request.Priority,
            request.CreatedTick,
            JobDiffSystemOrder.Transport,
            request.RequestorId,
            (ulong)sequence,
            request.Seed,
            request.PathSearchAttempt,
            rankedWorkerIds.ToImmutableArray());
    }

    private TransportMovementCursorReadRow? CaptureMovementCursor(Guid creatureId)
    {
        var cursor = _move.GetCursorSnapshot(DiffTargetEncoding.EntityKey(creatureId));
        if (!cursor.HasValue)
            return null;

        var value = cursor.Value;
        return new TransportMovementCursorReadRow(
            creatureId,
            value.EntityKey,
            value.Revision,
            value.Position,
            value.Request.Destination,
            value.Path.Hash,
            value.CurrentStep,
            value.Path.Steps.Length,
            value.StepWait,
            value.StuckTicks,
            value.LastProgress,
            ToNavigationChunk(value.Position),
            unchecked((ulong)value.LastConnectivityVersion));
    }

    private static ChunkKey ToNavigationChunk(Point3 point)
    {
        const int size = HumanFortress.Simulation.World.Chunk.SIZE_XY;
        return new ChunkKey(FloorDiv(point.X, size), FloorDiv(point.Y, size), point.Z);
    }

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private sealed record PreparedTransportTick(
        ulong Tick,
        TransportPlanningSnapshot Snapshot,
        TransportPlanResolution Resolution,
        ImmutableDictionary<Guid, MovementProposalData> MovementProposals);

    private readonly record struct StockpilePlanningObservations(
        IReadOnlyList<TransportStockpileCellReadRow> Cells,
        IReadOnlyList<Point3> NonStockpileDestinations);
}
