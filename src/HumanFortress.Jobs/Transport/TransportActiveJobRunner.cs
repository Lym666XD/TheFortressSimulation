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
    private readonly TransportReplanHandler _replanHandler;
    private readonly TransportJobFinalizer _jobFinalizer;
    private readonly TransportPickupHandler _pickupHandler;
    private readonly TransportDeliveryHandler _deliveryHandler;
    private readonly string _systemId;
    private readonly int _creatureReserveTtlTicks;

    internal TransportActiveJobRunner(
        WorldModel world,
        IWorldNavigationView navView,
        IMovementExecutor move,
        ITransportMovementDiffEmitter movementDiffEmitter,
        ITransportItemDiffEmitter itemDiffEmitter,
        TransportReplanHandler replanHandler,
        TransportJobFinalizer jobFinalizer,
        TransportPickupHandler pickupHandler,
        TransportDeliveryHandler deliveryHandler,
        string systemId,
        int creatureReserveTtlTicks)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));
        _movementDiffEmitter = movementDiffEmitter ?? throw new ArgumentNullException(nameof(movementDiffEmitter));
        _itemDiffEmitter = itemDiffEmitter ?? throw new ArgumentNullException(nameof(itemDiffEmitter));
        _replanHandler = replanHandler ?? throw new ArgumentNullException(nameof(replanHandler));
        _jobFinalizer = jobFinalizer ?? throw new ArgumentNullException(nameof(jobFinalizer));
        _pickupHandler = pickupHandler ?? throw new ArgumentNullException(nameof(pickupHandler));
        _deliveryHandler = deliveryHandler ?? throw new ArgumentNullException(nameof(deliveryHandler));
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
        _creatureReserveTtlTicks = creatureReserveTtlTicks;
    }

    internal void RunWriteTick(IEnumerable<ActiveJob> activeJobs, ulong tick, ICollection<ActiveJob> finished)
    {
        foreach (var job in activeJobs)
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

                _jobFinalizer.Finish(job, finished);
                continue;
            }

            uint entityId = DiffTargetEncoding.EntityId(cr.Guid);
            _world.Reservations.TryReserveCreature(job.CreatureId, _systemId, tick, tick + (ulong)_creatureReserveTtlTicks, jobId: $"haul:{job.ItemId}");

            var update = _move.UpdateMovement(entityId, _navView);
            if (update.NeedsReplan)
            {
                Point3 goal = job.Stage == JobStage.ToItem ? GetItemPos(job) : job.Dest;
                _replanHandler.HandleReplan(job, tick, entityId, update, goal);
                continue;
            }

            _movementDiffEmitter.MoveCreature(entityId, update.Position);

            if (update.Status == MovementStatus.Arrived || update.Status == MovementStatus.PathComplete)
            {
                if (job.Stage == JobStage.ToItem)
                {
                    _pickupHandler.HandleArrivedAtItem(job, tick, entityId, update.Position, finished);
                }
                else if (job.Stage == JobStage.ToDest)
                {
                    _deliveryHandler.HandleArrivedAtDestination(job, tick, update.Position, finished);
                }
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
