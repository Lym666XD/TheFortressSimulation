using HumanFortress.Contracts.Navigation;
using System;
using System.Collections.Generic;
using HumanFortress.Core.Simulation;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Transport;

internal sealed class TransportActiveJobRunner
{
    private readonly WorldModel _world;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly ITransportMovementDiffEmitter _movementDiffEmitter;
    private readonly ITransportItemDiffEmitter _itemDiffEmitter;
    private readonly ITransportStockpileIndexEmitter _stockpileIndexEmitter;
    private readonly TransportReplanHandler _replanHandler;
    private readonly TransportJobFinalizer _jobFinalizer;
    private readonly TransportPickupHandler _pickupHandler;
    private readonly TransportDeliveryHandler _deliveryHandler;
    private readonly int _creatureReserveTtlTicks;

    internal TransportActiveJobRunner(
        WorldModel world,
        IWorldNavigationView navView,
        IMovementExecutor move,
        ITransportMovementDiffEmitter movementDiffEmitter,
        ITransportItemDiffEmitter itemDiffEmitter,
        ITransportStockpileIndexEmitter? stockpileIndexEmitter,
        TransportReplanHandler replanHandler,
        TransportJobFinalizer jobFinalizer,
        TransportPickupHandler pickupHandler,
        TransportDeliveryHandler deliveryHandler,
        int creatureReserveTtlTicks)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));
        _movementDiffEmitter = movementDiffEmitter ?? throw new ArgumentNullException(nameof(movementDiffEmitter));
        _itemDiffEmitter = itemDiffEmitter ?? throw new ArgumentNullException(nameof(itemDiffEmitter));
        _stockpileIndexEmitter = stockpileIndexEmitter ?? NullTransportStockpileIndexEmitter.Instance;
        _replanHandler = replanHandler ?? throw new ArgumentNullException(nameof(replanHandler));
        _jobFinalizer = jobFinalizer ?? throw new ArgumentNullException(nameof(jobFinalizer));
        _pickupHandler = pickupHandler ?? throw new ArgumentNullException(nameof(pickupHandler));
        _deliveryHandler = deliveryHandler ?? throw new ArgumentNullException(nameof(deliveryHandler));
        _creatureReserveTtlTicks = creatureReserveTtlTicks;
    }

    internal void RunWriteTick(IEnumerable<ActiveJob> activeJobs, ulong tick, ICollection<ActiveJob> finished)
    {
        foreach (var job in activeJobs)
            RunJob(job, tick, plannedMovement: null, finished);
    }

    internal void RunPlannedWriteTick(
        ActiveJob job,
        ulong tick,
        MovementProposalData movement,
        ICollection<ActiveJob> finished)
    {
        RunJob(job, tick, movement, finished);
    }

    internal void ContinuePlannedPendingSplit(
        ActiveJob job,
        ulong tick,
        ICollection<ActiveJob> finished)
    {
        if (!job.PendingSplitReservation.IsValid)
            throw new InvalidOperationException("Transport pending-split decision lost its staged reservation.");

        RunJob(job, tick, plannedMovement: null, finished);
    }

    internal void RunPlannedReplan(
        ActiveJob job,
        ulong tick,
        Point3 source,
        Point3 goal,
        byte searchAttempt,
        ICollection<ActiveJob> finished)
    {
        var creature = _world.Creatures.GetInstance(job.CreatureId);
        if (creature == null || _world.Items.GetInstance(job.ItemId) == null)
        {
            CleanupInvalid(job, finished);
            return;
        }

        ulong expireTick = tick + (ulong)_creatureReserveTtlTicks;
        if (!_world.Reservations.TryRenewCreature(job.CreatureReservation, tick, expireTick)
            || !_world.Reservations.TryRenewItem(job.ItemReservation, tick, expireTick))
        {
            CleanupInvalid(job, finished);
            return;
        }

        ulong entityKey = DiffTargetEncoding.EntityKey(job.CreatureId);
        _move.CancelMovement(entityKey);
        _replanHandler.HandleReplan(
            job,
            tick,
            entityKey,
            new MovementUpdate(
                MovementStatus.TopologyChanged,
                source,
                NeedsReplan: true,
                LookAhead: null,
                SearchAttempt: searchAttempt),
            goal);
    }

    internal void CleanupInvalid(ActiveJob job, ICollection<ActiveJob> finished)
    {
        if (job.Stage == JobStage.ToDest
            && _world.Items.GetInstance(job.ItemId) is { } item)
        {
            _itemDiffEmitter.UnmarkCarried(
                job.ItemId,
                new Point3(item.Position.X, item.Position.Y, item.Z));
        }

        _move.CancelMovement(DiffTargetEncoding.EntityKey(job.CreatureId));
        _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
        _jobFinalizer.Finish(job, finished);
    }

    private void RunJob(
        ActiveJob job,
        ulong tick,
        MovementProposalData? plannedMovement,
        ICollection<ActiveJob> finished)
    {
        var cr = _world.Creatures.GetInstance(job.CreatureId);
            if (cr == null)
            {
                if (job.Stage == JobStage.ToDest)
                {
                    var itemInst = _world.Items.GetInstance(job.ItemId);
                    if (itemInst != null)
                    {
                        _itemDiffEmitter.UnmarkCarried(job.ItemId, new Point3(itemInst.Position.X, itemInst.Position.Y, itemInst.Z));
                    }
                }

                _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
                _jobFinalizer.Finish(job, finished);
                return;
            }

            ulong entityKey = DiffTargetEncoding.EntityKey(cr.Guid);
            ulong expireTick = tick + (ulong)_creatureReserveTtlTicks;
            if (!_world.Reservations.TryRenewCreature(job.CreatureReservation, tick, expireTick)
                || !_world.Reservations.TryRenewItem(job.ItemReservation, tick, expireTick))
            {
                _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
                _jobFinalizer.Finish(job, finished);
                return;
            }

            if (job.PendingSplitReservation.IsValid)
            {
                _pickupHandler.HandleArrivedAtItem(
                    job,
                    tick,
                    entityKey,
                    new Point3(cr.Position.X, cr.Position.Y, cr.Z),
                    finished);
                return;
            }

            MovementUpdate update;
            if (plannedMovement.HasValue)
            {
                var proposal = plannedMovement.Value;
                if (proposal.EntityKey != entityKey || !_move.TryCommitMovement(proposal))
                {
                    throw new InvalidOperationException(
                        $"Transport movement proposal for {job.CreatureId} failed its commit CAS.");
                }

                update = proposal.Update;
            }
            else
            {
                update = _move.UpdateMovement(entityKey, _navView);
            }
            if (update.NeedsReplan)
            {
                job.PathSearchAttempt = update.SearchAttempt;
                Point3 goal = job.Stage == JobStage.ToItem ? GetItemPos(job) : job.Dest;
                _replanHandler.HandleReplan(job, tick, entityKey, update, goal);
                return;
            }

            _movementDiffEmitter.MoveCreature(job.CreatureId, update.Position);

            if (update.Status == MovementStatus.Arrived)
            {
                job.PathSearchAttempt = 0;
                if (job.Stage == JobStage.ToItem)
                {
                    _pickupHandler.HandleArrivedAtItem(job, tick, entityKey, update.Position, finished);
                }
                else if (job.Stage == JobStage.ToDest)
                {
                    _deliveryHandler.HandleArrivedAtDestination(job, tick, update.Position, finished);
                }
            }
    }

    private Point3 GetItemPos(ActiveJob job)
    {
        var it = _world.Items.GetInstance(job.ItemId);
        if (it == null)
        {
            return new Point3(0, 0, 0);
        }

        return new Point3(it.Position.X, it.Position.Y, it.Z);
    }
}
