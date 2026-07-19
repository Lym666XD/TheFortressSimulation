using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Transport.Planning;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Stockpile;

namespace HumanFortress.Jobs.Transport;

internal enum TransportCommitStage : byte
{
    Assignments,
    Movement,
    Finalization
}

internal sealed partial class TransportJobExecutor
{
    private Action<TransportCommitStage>? _commitProbe;

    internal void SetCommitProbe(Action<TransportCommitStage>? probe) => _commitProbe = probe;

    private void CommitPreparedTick(PreparedTransportTick prepared)
    {
        var queueMemento = _requests.GetStateSnapshot();
        var backlogMemento = _backlog.GetStateSnapshot();
        var activeMemento = _active.Select(CloneActiveJob).ToArray();
        var statsMemento = _statsTracker.CaptureMemento();
        int intakeMemento = LastIntakeCount;
        using var reservationScope = _world.Reservations.BeginMutationScope();
        using var movementScope = _move.BeginMutationScope();
        using var sideEffectScope = _commitMutations.BeginMutationScope();

        try
        {
            int assigned = CommitAssignments(prepared);
            LastIntakeCount = assigned;
            _commitProbe?.Invoke(TransportCommitStage.Assignments);

            var finished = new List<ActiveJob>();
            CommitActiveDecisions(prepared, finished);
            _commitProbe?.Invoke(TransportCommitStage.Movement);

            foreach (var job in finished.Distinct())
                _active.Remove(job);
            if (finished.Count > 0)
                _statsTracker.RecordFinishedJobs();

            int carryoverOld = _backlog.CountOlderThan(
                prepared.Tick,
                _carryoverMaxTicks * 2);
            _statsTracker.RecordRead(
                LastIntakeCount,
                _active.Count,
                _backlog.Count,
                carryoverOld);
            _commitProbe?.Invoke(TransportCommitStage.Finalization);

            reservationScope.Commit();
            movementScope.Commit();
            sideEffectScope.Commit();
        }
        catch
        {
            _requests.RestoreStateSnapshot(queueMemento);
            _backlog.RestoreStateSnapshot(backlogMemento);
            _active.Clear();
            _active.AddRange(activeMemento.Select(CloneActiveJob));
            _statsTracker.RestoreMemento(statsMemento);
            LastIntakeCount = intakeMemento;
            throw;
        }
    }

    private int CommitAssignments(PreparedTransportTick prepared)
    {
        int intakeBudget = GetEffectiveIntakeBudget();
        if (intakeBudget <= 0)
            return 0;

        var creatures = _world.Creatures.GetAllInstances()
            .ToDictionary(static creature => creature.Guid);
        int allowedActive = GetAllowedActiveCount(creatures.Count);
        var busy = _active.Select(static job => job.CreatureId).ToHashSet();
        int assigned = 0;

        foreach (var plan in prepared.Resolution.AssignmentPlans)
        {
            if (assigned >= intakeBudget
                || (allowedActive != int.MaxValue && _active.Count >= allowedActive)
                || !TryResolvePlannedRequest(
                    prepared,
                    plan.Winner,
                    out var request,
                    out var backlogTick))
            {
                continue;
            }

            ActiveJob? job = null;
            bool increasePathSearchAttempt = false;
            foreach (var candidate in plan.OrderedCandidates)
            {
                if (!creatures.TryGetValue(candidate.CreatureId, out var worker)
                    || busy.Contains(candidate.CreatureId)
                    || !_assignmentHandler.MatchesPlannedAssignment(
                        candidate,
                        in request,
                        worker))
                {
                    continue;
                }

                job = _assignmentHandler.TryAssignPlanned(
                    request,
                    worker,
                    busy,
                    prepared.Tick,
                    out bool candidateIncreasesAttempt);
                increasePathSearchAttempt |= candidateIncreasesAttempt;
                if (job != null)
                    break;
            }

            if (job == null)
            {
                if (increasePathSearchAttempt
                    && TryConsumePlannedRequest(plan.Winner.Source, request, backlogTick))
                {
                    var retry = request with
                    {
                        PathSearchAttempt = request.PathSearchAttempt >= PathRequest.MaxSearchAttempt
                            ? PathRequest.MaxSearchAttempt
                            : (byte)(request.PathSearchAttempt + 1)
                    };
                    if (_backlog.TryEnqueue(retry, prepared.Tick))
                        _statsTracker.RecordRequeued();
                }
                continue;
            }

            if (!TryConsumePlannedRequest(plan.Winner.Source, request, backlogTick))
            {
                throw new InvalidOperationException(
                    $"Transport request {request.ItemGuid} changed between Plan and Commit.");
            }

            _active.Add(job);
            busy.Add(job.CreatureId);
            assigned++;
        }

        return assigned;
    }

    private void CommitActiveDecisions(
        PreparedTransportTick prepared,
        ICollection<ActiveJob> finished)
    {
        foreach (var snapshot in prepared.Snapshot.ActiveJobs)
        {
            var jobs = _active
                .Where(job => job.ItemId == snapshot.ItemId
                    && job.CreatureId == snapshot.CreatureId)
                .ToArray();
            if (jobs.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Transport active job {snapshot.ItemId}/{snapshot.CreatureId} changed between Plan and Commit.");
            }

            var decisions = prepared.Resolution.ActiveDecisions
                .Where(decision => decision.ItemId == snapshot.ItemId
                    && decision.CreatureId == snapshot.CreatureId)
                .ToArray();
            if (decisions.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Transport planner produced {decisions.Length} decisions for active job {snapshot.ItemId}/{snapshot.CreatureId}.");
            }

            var job = jobs[0];
            var decision = decisions[0];
            if (!MatchesActiveSnapshot(job, snapshot))
                continue;

            if (decision.Kind == TransportActiveDecisionKind.RejectedNoMutation)
                continue;

            if (decision.Kind == TransportActiveDecisionKind.CleanupInvalid)
            {
                if (StillMatchesCleanupReason(snapshot, decision.Rejection))
                    _activeJobRunner.CleanupInvalid(job, finished);
                continue;
            }

            var intent = decision.AcceptedIntent
                ?? throw new InvalidOperationException(
                    $"Accepted transport active decision {decision.Kind} has no intent.");
            if (!MatchesIntentExpectations(intent)
                || !MatchesPendingSplitExpectation(job, intent.ExpectedPendingSplit))
            {
                continue;
            }

            switch (decision.Kind)
            {
                case TransportActiveDecisionKind.AdvanceMovement:
                    if (!prepared.MovementProposals.TryGetValue(job.CreatureId, out var movement)
                        || movement.EntityKey != intent.ExpectedCreature.EntityKey
                        || movement.ExpectedRevision != intent.ExpectedMovement.CursorRevision)
                    {
                        throw new InvalidOperationException(
                            $"Transport movement proposal for {job.CreatureId} does not match its accepted intent.");
                    }

                    _activeJobRunner.RunPlannedWriteTick(
                        job,
                        prepared.Tick,
                        movement,
                        finished);
                    break;

                case TransportActiveDecisionKind.ReplanMovement:
                    _activeJobRunner.RunPlannedReplan(
                        job,
                        prepared.Tick,
                        intent.SourcePosition,
                        intent.Destination,
                        intent.PathSearchAttempt,
                        finished);
                    break;

                case TransportActiveDecisionKind.ContinuePendingSplit:
                    _activeJobRunner.ContinuePlannedPendingSplit(
                        job,
                        prepared.Tick,
                        finished);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported accepted transport active decision {decision.Kind}.");
            }
        }
    }

    private bool MatchesIntentExpectations(TransportIntent intent)
    {
        var item = _world.Items.GetInstance(intent.ItemId);
        if (item == null
            || DiffTargetEncoding.EntityKey(item.Guid) != intent.ExpectedItem.EntityKey
            || new Point3(item.Position.X, item.Position.Y, item.Z) != intent.ExpectedItem.Position
            || item.StackCount != intent.ExpectedItem.StackCount
            || item.IsOnGround != intent.ExpectedItem.IsOnGround
            || (item.CarriedBy ?? Guid.Empty) != intent.ExpectedItem.CarrierId)
        {
            return false;
        }

        var creature = _world.Creatures.GetInstance(intent.CreatureId);
        if (creature == null
            || DiffTargetEncoding.EntityKey(creature.Guid) != intent.ExpectedCreature.EntityKey
            || new Point3(creature.Position.X, creature.Position.Y, creature.Z)
                != intent.ExpectedCreature.Position
            || creature.HP != intent.ExpectedCreature.HitPoints)
        {
            return false;
        }

        if (!MatchesStockpileExpectation(intent.ExpectedStockpile)
            || unchecked((ulong)_navView.GetConnectivityVersion(
                    intent.ExpectedNavigation.StartChunk))
                != intent.ExpectedNavigation.StartConnectivityVersion
            || unchecked((ulong)_navView.GetConnectivityVersion(
                    intent.ExpectedNavigation.GoalChunk))
                != intent.ExpectedNavigation.GoalConnectivityVersion)
        {
            return false;
        }

        var cursor = _move.GetCursorSnapshot(intent.ExpectedCreature.EntityKey);
        if (!intent.ExpectedMovement.IsRequired)
            return !cursor.HasValue;
        if (!cursor.HasValue)
            return false;

        var current = cursor.Value;
        return current.EntityKey == intent.ExpectedCreature.EntityKey
            && current.Revision == intent.ExpectedMovement.CursorRevision
            && current.Position == intent.ExpectedMovement.Position
            && current.Request.Destination == intent.ExpectedMovement.Destination
            && current.Path.Hash == intent.ExpectedMovement.PathHash
            && current.CurrentStep == intent.ExpectedMovement.CurrentStep
            && current.Path.Steps.Length == intent.ExpectedMovement.StepCount;
    }

    private bool MatchesStockpileExpectation(TransportStockpileExpectation expectation)
    {
        if (expectation.Kind == TransportStockpileExpectationKind.None)
            return true;
        if (expectation.Kind == TransportStockpileExpectationKind.Absent)
        {
            return !StockpileWorldQueries.TryGetStockpileCell(
                _world,
                expectation.Position.X,
                expectation.Position.Y,
                expectation.Position.Z,
                out _);
        }

        if (!StockpileWorldQueries.TryGetStockpileCell(
                _world,
                expectation.Position.X,
                expectation.Position.Y,
                expectation.Position.Z,
                out var location)
            || location.ZoneId != expectation.ZoneId)
        {
            return false;
        }

        var zone = _world.Stockpiles.GetZone(location.ZoneId);
        if (zone == null || zone.Generation != expectation.Generation)
            return false;

        Guid occupying = _world.Items
            .GetGroundItemsAt(
                new SadRogue.Primitives.Point(expectation.Position.X, expectation.Position.Y),
                expectation.Position.Z)
            .Select(static item => item.Guid)
            .FirstOrDefault();
        return occupying == expectation.OccupyingItemId;
    }

    private static bool MatchesActiveSnapshot(
        ActiveJob job,
        TransportActiveJobReadRow snapshot)
    {
        return job.ItemId == snapshot.ItemId
            && job.CreatureId == snapshot.CreatureId
            && job.Dest == snapshot.Destination
            && job.Stage == snapshot.Stage
            && job.Quantity == snapshot.Quantity
            && job.Reason == snapshot.Reason
            && job.InvalidReplanCount == snapshot.InvalidReplanCount
            && job.PathSearchAttempt == snapshot.PathSearchAttempt
            && job.PendingSplitReservation.ResourceId == snapshot.PendingSplitItemId
            && job.PendingSplitReservation.Generation == snapshot.PendingSplitGeneration
            && job.PendingSplitIssuedTick == snapshot.PendingSplitIssuedTick;
    }

    private static bool MatchesPendingSplitExpectation(
        ActiveJob job,
        TransportPendingSplitExpectation expectation)
    {
        return !expectation.IsRequired
            || (job.PendingSplitReservation.IsValid
                && job.PendingSplitReservation.ResourceId == expectation.ItemId
                && job.PendingSplitReservation.Generation == expectation.ReservationGeneration
                && job.PendingSplitIssuedTick == expectation.IssuedTick);
    }

    private bool StillMatchesCleanupReason(
        TransportActiveJobReadRow snapshot,
        TransportIntentRejection? rejection)
    {
        if (!rejection.HasValue)
            return false;

        return rejection.Value.Reason switch
        {
            TransportIntentRejectionReason.MissingItem =>
                _world.Items.GetInstance(snapshot.ItemId) == null,
            TransportIntentRejectionReason.MissingCreature =>
                _world.Creatures.GetInstance(snapshot.CreatureId) == null,
            TransportIntentRejectionReason.MissingStockpileCell =>
                !StockpileWorldQueries.TryGetStockpileCell(
                    _world,
                    snapshot.Destination.X,
                    snapshot.Destination.Y,
                    snapshot.Destination.Z,
                    out _),
            _ => false
        };
    }

    private bool TryResolvePlannedRequest(
        PreparedTransportTick prepared,
        TransportIntent intent,
        out TransportRequest request,
        out ulong backlogTick)
    {
        backlogTick = 0;
        if (intent.Source == TransportPendingSource.Queue)
        {
            foreach (var row in prepared.Snapshot.QueuedRequests)
            {
                if (row.Request.ItemId != intent.ItemId)
                    continue;
                request = ToRequest(row.Request);
                return true;
            }
        }
        else
        {
            foreach (var row in prepared.Snapshot.BacklogRequests)
            {
                if (row.Request.ItemId != intent.ItemId)
                    continue;
                request = ToRequest(row.Request);
                backlogTick = row.EnqueuedTick;
                return true;
            }
        }

        request = default;
        return false;
    }

    private bool TryConsumePlannedRequest(
        TransportPendingSource source,
        TransportRequest request,
        ulong backlogTick)
    {
        return source == TransportPendingSource.Queue
            ? _requests.TryConsume(in request)
            : _backlog.TryConsume(in request, backlogTick);
    }

    private static TransportRequest ToRequest(TransportRequestReadRow row)
    {
        return new TransportRequest(
            row.ItemId,
            new SadRogue.Primitives.Point(row.Source.X, row.Source.Y),
            row.Source.Z,
            new SadRogue.Primitives.Point(row.Destination.X, row.Destination.Y),
            row.Destination.Z,
            row.Quantity,
            row.Reason,
            row.Priority,
            row.ProducerId,
            row.CreatedTick,
            row.Seed,
            row.PathSearchAttempt);
    }

    private static ActiveJob CloneActiveJob(ActiveJob source)
    {
        return new ActiveJob
        {
            CreatureId = source.CreatureId,
            ItemId = source.ItemId,
            Dest = source.Dest,
            Stage = source.Stage,
            Quantity = source.Quantity,
            InvalidReplanCount = source.InvalidReplanCount,
            Reason = source.Reason,
            PathSearchAttempt = source.PathSearchAttempt,
            CreatureReservation = source.CreatureReservation,
            ItemReservation = source.ItemReservation,
            PendingSplitReservation = source.PendingSplitReservation,
            PendingSplitIssuedTick = source.PendingSplitIssuedTick
        };
    }
}
