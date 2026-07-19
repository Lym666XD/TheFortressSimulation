using System.Collections.Immutable;
using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport.Planning;

internal enum TransportPlanningWorkKind : byte
{
    ActiveJob,
    QueuedRequest,
    BacklogRequest
}

internal readonly record struct TransportPlanningWorkItem(
    int CanonicalOrder,
    TransportPlanningWorkKind Kind,
    TransportPendingSource PendingSource,
    TransportRequestReadRow Request,
    TransportActiveJobReadRow ActiveJob);

internal readonly record struct TransportIntentBatch(
    ImmutableArray<TransportIntent> Intents,
    ImmutableArray<TransportIntentRejection> Rejections);

/// <summary>
/// Stateless transport planner over a captured read model. It cannot consume
/// request queues or reach mutable authority, reservation, path, or movement owners.
/// </summary>
internal static class TransportPurePlanner
{
    internal static TransportIntentBatch Plan(
        TransportPlanningSnapshot snapshot,
        ImmutableArray<TransportPlanningWorkItem> workItems)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var intents = ImmutableArray.CreateBuilder<TransportIntent>();
        var rejections = ImmutableArray.CreateBuilder<TransportIntentRejection>();
        var itemById = snapshot.Items.ToDictionary(static item => item.ItemId);
        var creatureById = snapshot.Creatures.ToDictionary(static creature => creature.CreatureId);
        var reservationByResource = snapshot.Reservations
            .Where(reservation => snapshot.Tick <= reservation.ExpireTick)
            .ToDictionary(
                static reservation => (reservation.ResourceKind, reservation.ResourceId));
        var stockpileByPosition = snapshot.StockpileCells.ToDictionary(static cell => cell.Position);
        var observedNonStockpileDestinations = snapshot.ObservedNonStockpileDestinations.ToHashSet();
        var revisionByChunk = snapshot.NavigationRevisions.ToDictionary(static revision => revision.Chunk);
        var cursorByCreature = snapshot.MovementCursors.ToDictionary(static cursor => cursor.CreatureId);
        var activeItems = snapshot.ActiveJobs.Select(static job => job.ItemId).ToHashSet();
        var activeCreatures = snapshot.ActiveJobs.Select(static job => job.CreatureId).ToHashSet();

        foreach (var workItem in workItems.OrderBy(static item => item.CanonicalOrder))
        {
            if (workItem.Kind == TransportPlanningWorkKind.ActiveJob)
            {
                PlanActiveJob(
                    snapshot,
                    workItem,
                    itemById,
                    creatureById,
                    cursorByCreature,
                    revisionByChunk,
                    stockpileByPosition,
                    observedNonStockpileDestinations,
                    intents,
                    rejections);
                continue;
            }

            PlanPendingRequest(
                snapshot,
                workItem,
                itemById,
                creatureById,
                reservationByResource,
                stockpileByPosition,
                observedNonStockpileDestinations,
                revisionByChunk,
                cursorByCreature,
                activeItems,
                activeCreatures,
                intents,
                rejections);
        }

        return new TransportIntentBatch(intents.ToImmutable(), rejections.ToImmutable());
    }

    private static void PlanPendingRequest(
        TransportPlanningSnapshot snapshot,
        TransportPlanningWorkItem workItem,
        IReadOnlyDictionary<Guid, TransportItemReadRow> itemById,
        IReadOnlyDictionary<Guid, TransportCreatureReadRow> creatureById,
        IReadOnlyDictionary<(TransportReservationResourceKind, Guid), TransportReservationReadRow> reservationByResource,
        IReadOnlyDictionary<Point3, TransportStockpileCellReadRow> stockpileByPosition,
        IReadOnlySet<Point3> observedNonStockpileDestinations,
        IReadOnlyDictionary<ChunkKey, TransportNavigationRevisionReadRow> revisionByChunk,
        IReadOnlyDictionary<Guid, TransportMovementCursorReadRow> cursorByCreature,
        IReadOnlySet<Guid> activeItems,
        IReadOnlySet<Guid> activeCreatures,
        ImmutableArray<TransportIntent>.Builder intents,
        ImmutableArray<TransportIntentRejection>.Builder rejections)
    {
        var request = workItem.Request;
        var baseOrder = CreateOrderKey(request, workItem.CanonicalOrder, candidateOrder: 0);
        if (request.ItemId == Guid.Empty
            || request.Quantity <= 0
            || request.Source == request.Destination
            || string.IsNullOrWhiteSpace(request.ProducerId))
        {
            rejections.Add(CreatePlanningRejection(
                baseOrder,
                TransportIntentKind.AssignRequest,
                request.ItemId,
                Guid.Empty,
                TransportIntentRejectionReason.InvalidRequest,
                "invalid-request"));
            return;
        }

        if (!itemById.TryGetValue(request.ItemId, out var item))
        {
            rejections.Add(CreatePlanningRejection(
                baseOrder,
                TransportIntentKind.AssignRequest,
                request.ItemId,
                Guid.Empty,
                TransportIntentRejectionReason.MissingItem,
                "missing-item"));
            return;
        }

        if (!item.IsOnGround
            || item.CarrierId != Guid.Empty
            || item.StackCount < request.Quantity
            || item.Position != request.Source
            || activeItems.Contains(item.ItemId))
        {
            rejections.Add(CreatePlanningRejection(
                baseOrder,
                TransportIntentKind.AssignRequest,
                request.ItemId,
                Guid.Empty,
                TransportIntentRejectionReason.ItemUnavailable,
                "item-unavailable"));
            return;
        }

        if (reservationByResource.ContainsKey((TransportReservationResourceKind.Item, item.ItemId)))
        {
            rejections.Add(CreatePlanningRejection(
                baseOrder,
                TransportIntentKind.AssignRequest,
                request.ItemId,
                Guid.Empty,
                TransportIntentRejectionReason.ItemAlreadyReserved,
                "item-reserved"));
            return;
        }

        var stockpileExpectation = TransportStockpileExpectation.None;
        var claims = ImmutableArray.CreateBuilder<TransportIntentResourceClaim>();
        claims.Add(TransportIntentResourceClaim.Item(item.ItemId));
        if (TransportDestinationValidator.WritesStockpileIndex(request.Reason))
        {
            if (!stockpileByPosition.TryGetValue(request.Destination, out var stockpile))
            {
                if (!observedNonStockpileDestinations.Contains(request.Destination)
                    || TransportDestinationValidator.RequiresStockpileCell(request.Reason))
                {
                    rejections.Add(CreatePlanningRejection(
                        baseOrder,
                        TransportIntentKind.AssignRequest,
                        request.ItemId,
                        Guid.Empty,
                        TransportIntentRejectionReason.MissingStockpileCell,
                        observedNonStockpileDestinations.Contains(request.Destination)
                            ? "missing-stockpile-cell"
                            : "missing-stockpile-observation"));
                    return;
                }

                stockpileExpectation = TransportStockpileExpectation.Absent(request.Destination);
            }
            else
            {
                if (!stockpile.Accepts(item.DefinitionId))
                {
                    rejections.Add(CreatePlanningRejection(
                        baseOrder,
                        TransportIntentKind.AssignRequest,
                        request.ItemId,
                        Guid.Empty,
                        TransportIntentRejectionReason.StockpileFilterRejected,
                        "stockpile-filter-rejected"));
                    return;
                }

                if (stockpile.OccupyingItemId != Guid.Empty
                    && stockpile.OccupyingItemId != item.ItemId)
                {
                    rejections.Add(CreatePlanningRejection(
                        baseOrder,
                        TransportIntentKind.AssignRequest,
                        request.ItemId,
                        Guid.Empty,
                        TransportIntentRejectionReason.StockpileCellOccupied,
                        "stockpile-cell-occupied"));
                    return;
                }

                stockpileExpectation = TransportStockpileExpectation.Present(
                    stockpile.ZoneId,
                    stockpile.Position,
                    stockpile.Generation,
                    stockpile.OccupyingItemId);
                claims.Add(TransportIntentResourceClaim.Stockpile(request.Destination));
            }
        }

        var goalChunk = ToChunkKey(request.Source);
        if (!revisionByChunk.TryGetValue(goalChunk, out var goalRevision))
        {
            rejections.Add(CreatePlanningRejection(
                baseOrder,
                TransportIntentKind.AssignRequest,
                request.ItemId,
                Guid.Empty,
                TransportIntentRejectionReason.NavigationRevisionChanged,
                "missing-navigation-revision"));
            return;
        }

        IEnumerable<TransportCreatureReadRow> candidateCreatures = request.RankedWorkerIds.IsDefault
            ? snapshot.Creatures
            : request.RankedWorkerIds
                .Where(creatureById.ContainsKey)
                .Select(workerId => creatureById[workerId]);
        int candidateOrder = 0;
        foreach (var creature in candidateCreatures)
        {
            if (creature.HitPoints <= 0
                || !creature.CanHaul
                || activeCreatures.Contains(creature.CreatureId)
                || cursorByCreature.ContainsKey(creature.CreatureId)
                || reservationByResource.ContainsKey((TransportReservationResourceKind.Creature, creature.CreatureId)))
            {
                continue;
            }

            var startChunk = ToChunkKey(creature.Position);
            if (!revisionByChunk.TryGetValue(startChunk, out var startRevision))
                continue;

            candidateOrder++;
            var candidateClaims = claims.ToImmutable().AddRange(new[]
            {
                TransportIntentResourceClaim.Creature(creature.CreatureId),
                TransportIntentResourceClaim.Movement(creature.CreatureId)
            });
            intents.Add(TransportIntent.Create(
                CreateOrderKey(request, workItem.CanonicalOrder, candidateOrder),
                TransportIntentKind.AssignRequest,
                workItem.PendingSource,
                item.ItemId,
                creature.CreatureId,
                item.Position,
                request.Destination,
                request.Quantity,
                request.Reason,
                request.PathSearchAttempt,
                workItem.CanonicalOrder,
                candidateOrder,
                new TransportItemExpectation(
                    item.EntityKey,
                    item.Position,
                    item.StackCount,
                    item.IsOnGround,
                    item.CarrierId),
                new TransportCreatureExpectation(
                    creature.EntityKey,
                    creature.Position,
                    creature.HitPoints),
                stockpileExpectation,
                TransportPendingSplitExpectation.None,
                new TransportNavigationExpectation(
                    startChunk,
                    startRevision.ConnectivityVersion,
                    goalChunk,
                    goalRevision.ConnectivityVersion),
                TransportMovementExpectation.None,
                candidateClaims));
        }

        if (candidateOrder == 0)
        {
            rejections.Add(CreatePlanningRejection(
                baseOrder,
                TransportIntentKind.AssignRequest,
                request.ItemId,
                Guid.Empty,
                TransportIntentRejectionReason.NoEligibleCreature,
                "no-eligible-creature"));
        }
    }

    private static void PlanActiveJob(
        TransportPlanningSnapshot snapshot,
        TransportPlanningWorkItem workItem,
        IReadOnlyDictionary<Guid, TransportItemReadRow> itemById,
        IReadOnlyDictionary<Guid, TransportCreatureReadRow> creatureById,
        IReadOnlyDictionary<Guid, TransportMovementCursorReadRow> cursorByCreature,
        IReadOnlyDictionary<ChunkKey, TransportNavigationRevisionReadRow> revisionByChunk,
        IReadOnlyDictionary<Point3, TransportStockpileCellReadRow> stockpileByPosition,
        IReadOnlySet<Point3> observedNonStockpileDestinations,
        ImmutableArray<TransportIntent>.Builder intents,
        ImmutableArray<TransportIntentRejection>.Builder rejections)
    {
        var job = workItem.ActiveJob;
        var order = CreateOrderKey(job, workItem.CanonicalOrder);
        if (!itemById.TryGetValue(job.ItemId, out var item))
        {
            rejections.Add(CreatePlanningRejection(
                order,
                TransportIntentKind.AdvanceMovement,
                job.ItemId,
                job.CreatureId,
                TransportIntentRejectionReason.MissingItem,
                "active-missing-item"));
            return;
        }

        if (!creatureById.TryGetValue(job.CreatureId, out var creature))
        {
            rejections.Add(CreatePlanningRejection(
                order,
                TransportIntentKind.AdvanceMovement,
                job.ItemId,
                job.CreatureId,
                TransportIntentRejectionReason.MissingCreature,
                "active-missing-creature"));
            return;
        }

        var claims = ImmutableArray.CreateBuilder<TransportIntentResourceClaim>();
        claims.Add(TransportIntentResourceClaim.Item(job.ItemId));
        claims.Add(TransportIntentResourceClaim.Creature(job.CreatureId));

        var stockpileExpectation = TransportStockpileExpectation.None;
        if (TransportDestinationValidator.WritesStockpileIndex(job.Reason))
        {
            if (!stockpileByPosition.TryGetValue(job.Destination, out var stockpile))
            {
                if (!observedNonStockpileDestinations.Contains(job.Destination)
                    || TransportDestinationValidator.RequiresStockpileCell(job.Reason))
                {
                    rejections.Add(CreatePlanningRejection(
                        order,
                        TransportIntentKind.AdvanceMovement,
                        job.ItemId,
                        job.CreatureId,
                        TransportIntentRejectionReason.MissingStockpileCell,
                        observedNonStockpileDestinations.Contains(job.Destination)
                            ? "active-missing-stockpile-cell"
                            : "active-missing-stockpile-observation"));
                    return;
                }

                stockpileExpectation = TransportStockpileExpectation.Absent(job.Destination);
            }
            else
            {
                stockpileExpectation = TransportStockpileExpectation.Present(
                    stockpile.ZoneId,
                    stockpile.Position,
                    stockpile.Generation,
                    stockpile.OccupyingItemId);
                claims.Add(TransportIntentResourceClaim.Stockpile(job.Destination));
            }
        }

        if (job.HasPendingSplit)
        {
            var workerChunk = ToChunkKey(creature.Position);
            var destinationChunk = ToChunkKey(job.Destination);
            if (!revisionByChunk.TryGetValue(workerChunk, out var workerRevision)
                || !revisionByChunk.TryGetValue(destinationChunk, out var destinationRevision))
            {
                rejections.Add(CreatePlanningRejection(
                    order,
                    TransportIntentKind.ContinuePendingSplit,
                    job.ItemId,
                    job.CreatureId,
                    TransportIntentRejectionReason.NavigationRevisionChanged,
                    "pending-split-missing-navigation-revision"));
                return;
            }

            claims.Add(TransportIntentResourceClaim.Item(job.PendingSplitItemId));
            claims.Add(TransportIntentResourceClaim.Movement(job.CreatureId));
            var expectedMovement = cursorByCreature.TryGetValue(job.CreatureId, out var pendingCursor)
                ? new TransportMovementExpectation(
                    IsRequired: true,
                    pendingCursor.Revision,
                    pendingCursor.Position,
                    pendingCursor.Destination,
                    pendingCursor.PathHash,
                    pendingCursor.CurrentStep,
                    pendingCursor.StepCount)
                : TransportMovementExpectation.None;
            intents.Add(TransportIntent.Create(
                order,
                TransportIntentKind.ContinuePendingSplit,
                TransportPendingSource.Queue,
                job.ItemId,
                job.CreatureId,
                item.Position,
                job.Destination,
                job.Quantity,
                job.Reason,
                job.PathSearchAttempt,
                workItem.CanonicalOrder,
                candidateOrder: 0,
                new TransportItemExpectation(
                    item.EntityKey,
                    item.Position,
                    item.StackCount,
                    item.IsOnGround,
                    item.CarrierId),
                new TransportCreatureExpectation(
                    creature.EntityKey,
                    creature.Position,
                    creature.HitPoints),
                stockpileExpectation,
                new TransportPendingSplitExpectation(
                    IsRequired: true,
                    job.PendingSplitItemId,
                    job.PendingSplitGeneration,
                    job.PendingSplitIssuedTick),
                new TransportNavigationExpectation(
                    workerChunk,
                    workerRevision.ConnectivityVersion,
                    destinationChunk,
                    destinationRevision.ConnectivityVersion),
                expectedMovement,
                claims));
            return;
        }

        if (!cursorByCreature.TryGetValue(job.CreatureId, out var cursor))
        {
            var workerChunk = ToChunkKey(creature.Position);
            var goal = job.Stage == JobStage.ToItem ? item.Position : job.Destination;
            var goalChunkWithoutCursor = ToChunkKey(goal);
            if (!revisionByChunk.TryGetValue(workerChunk, out var workerRevision)
                || !revisionByChunk.TryGetValue(goalChunkWithoutCursor, out var goalRevisionWithoutCursor))
            {
                rejections.Add(CreatePlanningRejection(
                    order,
                    TransportIntentKind.ReplanMovement,
                    job.ItemId,
                    job.CreatureId,
                    TransportIntentRejectionReason.NavigationRevisionChanged,
                    "replan-missing-navigation-revision"));
                return;
            }

            claims.Add(TransportIntentResourceClaim.Movement(job.CreatureId));
            intents.Add(TransportIntent.Create(
                order,
                TransportIntentKind.ReplanMovement,
                TransportPendingSource.Queue,
                job.ItemId,
                job.CreatureId,
                creature.Position,
                goal,
                job.Quantity,
                job.Reason,
                job.PathSearchAttempt,
                workItem.CanonicalOrder,
                candidateOrder: 0,
                new TransportItemExpectation(
                    item.EntityKey,
                    item.Position,
                    item.StackCount,
                    item.IsOnGround,
                    item.CarrierId),
                new TransportCreatureExpectation(
                    creature.EntityKey,
                    creature.Position,
                    creature.HitPoints),
                stockpileExpectation,
                TransportPendingSplitExpectation.None,
                new TransportNavigationExpectation(
                    workerChunk,
                    workerRevision.ConnectivityVersion,
                    goalChunkWithoutCursor,
                    goalRevisionWithoutCursor.ConnectivityVersion),
                TransportMovementExpectation.None,
                claims));
            return;
        }

        var goalChunk = ToChunkKey(cursor.Destination);
        if (!revisionByChunk.TryGetValue(cursor.CurrentChunk, out var revision)
            || !revisionByChunk.TryGetValue(goalChunk, out var goalRevision))
        {
            rejections.Add(CreatePlanningRejection(
                order,
                TransportIntentKind.ReplanMovement,
                job.ItemId,
                job.CreatureId,
                TransportIntentRejectionReason.NavigationRevisionChanged,
                "navigation-revision-changed"));
            return;
        }

        claims.Add(TransportIntentResourceClaim.Movement(job.CreatureId));

        var intentKind = cursor.ExpectedConnectivityVersion != 0
            && revision.ConnectivityVersion != cursor.ExpectedConnectivityVersion
                ? TransportIntentKind.ReplanMovement
                : TransportIntentKind.AdvanceMovement;

        intents.Add(TransportIntent.Create(
            order,
            intentKind,
            TransportPendingSource.Queue,
            job.ItemId,
            job.CreatureId,
            cursor.Position,
            cursor.Destination,
            job.Quantity,
            job.Reason,
            job.PathSearchAttempt,
            workItem.CanonicalOrder,
            candidateOrder: 0,
            new TransportItemExpectation(
                item.EntityKey,
                item.Position,
                item.StackCount,
                item.IsOnGround,
                item.CarrierId),
            new TransportCreatureExpectation(
                creature.EntityKey,
                creature.Position,
                creature.HitPoints),
            stockpileExpectation,
            TransportPendingSplitExpectation.None,
            new TransportNavigationExpectation(
                cursor.CurrentChunk,
                revision.ConnectivityVersion,
                goalChunk,
                goalRevision.ConnectivityVersion),
            new TransportMovementExpectation(
                IsRequired: true,
                cursor.Revision,
                cursor.Position,
                cursor.Destination,
                cursor.PathHash,
                cursor.CurrentStep,
                cursor.StepCount),
            claims));
    }

    private static TransportIntentOrderKey CreateOrderKey(
        TransportRequestReadRow request,
        int canonicalOrder,
        int candidateOrder)
    {
        return new TransportIntentOrderKey(
            request.Priority,
            request.CreatedTick,
            request.SystemOrder,
            request.ProducerId,
            request.ItemId,
            CombineLocalSequence(canonicalOrder, candidateOrder));
    }

    private static TransportIntentOrderKey CreateOrderKey(
        TransportActiveJobReadRow job,
        int canonicalOrder)
    {
        return new TransportIntentOrderKey(
            job.Priority,
            job.CreatedTick,
            job.SystemOrder,
            job.ProducerId,
            job.ItemId,
            CombineLocalSequence(canonicalOrder, candidateOrder: 0));
    }

    private static ulong CombineLocalSequence(int canonicalOrder, int candidateOrder)
    {
        return ((ulong)(uint)canonicalOrder << 32) | (uint)candidateOrder;
    }

    private static TransportIntentRejection CreatePlanningRejection(
        TransportIntentOrderKey orderKey,
        TransportIntentKind kind,
        Guid itemId,
        Guid creatureId,
        TransportIntentRejectionReason reason,
        string detailCode)
    {
        return new TransportIntentRejection(
            orderKey,
            kind,
            itemId,
            creatureId,
            reason,
            ConflictingWinner: null,
            DetailCode: detailCode);
    }

    private static ChunkKey ToChunkKey(Point3 position)
    {
        const int chunkSize = 32;
        return new ChunkKey(
            FloorDiv(position.X, chunkSize),
            FloorDiv(position.Y, chunkSize),
            position.Z);
    }

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }
}
