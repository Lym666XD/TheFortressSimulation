using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Jobs;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Transport;

internal sealed class TransportPickupHandler
{
    private const string DropReasonInStockpile = "already in stockpile";

    private readonly WorldModel _world;
    private readonly TransportDestinationValidator _destinationValidator;
    private readonly IPathService _paths;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly ITransportItemDiffEmitter _diffEmitter;
    private readonly ITransportStockpileIndexEmitter _stockpileIndexEmitter;
    private readonly TransportJobFinalizer _jobFinalizer;
    private readonly ITransportJobLogger _logger;
    private readonly int _creatureReserveTtlTicks;
    private readonly Func<Guid, Guid, uint> _seedFrom;

    internal TransportPickupHandler(
        WorldModel world,
        TransportDestinationValidator destinationValidator,
        IPathService paths,
        IWorldNavigationView navView,
        IMovementExecutor move,
        ITransportItemDiffEmitter diffEmitter,
        ITransportStockpileIndexEmitter? stockpileIndexEmitter,
        TransportJobFinalizer jobFinalizer,
        ITransportJobLogger? logger,
        int creatureReserveTtlTicks,
        Func<Guid, Guid, uint> seedFrom)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _destinationValidator = destinationValidator ?? throw new ArgumentNullException(nameof(destinationValidator));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
        _stockpileIndexEmitter = stockpileIndexEmitter ?? NullTransportStockpileIndexEmitter.Instance;
        _jobFinalizer = jobFinalizer ?? throw new ArgumentNullException(nameof(jobFinalizer));
        _logger = logger ?? NullTransportJobLogger.Instance;
        _creatureReserveTtlTicks = creatureReserveTtlTicks;
        _seedFrom = seedFrom ?? throw new ArgumentNullException(nameof(seedFrom));
    }

    internal void HandleArrivedAtItem(ActiveJob job, ulong tick, ulong entityKey, Point3 workerPosition, ICollection<ActiveJob> finished)
    {
        if (job.Reason == TransportReason.ToStockpile && _destinationValidator.IsItemInStockpile(job.ItemId))
        {
            _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={job.ItemId} because already in stockpile ({DropReasonInStockpile})");
            _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
            _jobFinalizer.Finish(job, finished);
            return;
        }

        var inst = _world.Items.GetInstance(job.ItemId);
        if (inst == null || !inst.IsOnGround)
        {
            _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={job.ItemId} source missing or not on ground at pickup");
            _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
            _jobFinalizer.Finish(job, finished);
            return;
        }

        var currentItemPos = new Point3(inst.Position.X, inst.Position.Y, inst.Z);
        if (currentItemPos != workerPosition)
        {
            var retry = new PathRequest(workerPosition, currentItemPos, MoveMode.Walk, PathFlags.AllowDiagonal, _seedFrom(job.CreatureId, job.ItemId));
            IWorldNavigationView retryView = _navView;
            var retryPath = _paths.Solve(in retry, in retryView);
            if (retryPath.Kind == PathResultKind.Found)
            {
                _move.BeginMovement(entityKey, retry, retryPath);
                _logger.Log($"[TRANS-JOBS][{tick}] Repath to moved item={job.ItemId} from=({workerPosition.X},{workerPosition.Y},{workerPosition.Z}) to=({currentItemPos.X},{currentItemPos.Y},{currentItemPos.Z})");
                return;
            }

            _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={job.ItemId} moved to unreachable pickup=({currentItemPos.X},{currentItemPos.Y},{currentItemPos.Z}) kind={retryPath.Kind}");
            _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
            _jobFinalizer.Finish(job, finished);
            JobStats.NoPath++;
            return;
        }

        if (job.Quantity > 0 && inst.StackCount > job.Quantity)
        {
            var sourceItemId = job.ItemId;
            var newId = _diffEmitter.GenerateSplitStackItemGuid(sourceItemId, job.CreatureId, tick, job.Quantity);
            if (_world.Items.GetInstance(newId) != null)
            {
                _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={sourceItemId} split-id collision new={newId}");
                _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
                _jobFinalizer.Finish(job, finished);
                return;
            }

            if (!_world.Reservations.TryReserveItem(newId, job.CreatureId, tick, tick + (ulong)_creatureReserveTtlTicks))
            {
                _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={sourceItemId} split reservation failed new={newId}");
                _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
                _jobFinalizer.Finish(job, finished);
                return;
            }

            if (!_diffEmitter.SplitStack(sourceItemId, newId, inst.Position.X, inst.Position.Y, inst.Z, job.Quantity))
            {
                _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={sourceItemId} split emit failed take={job.Quantity}");
                _world.Reservations.ReleaseItem(newId);
                _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
                _jobFinalizer.Finish(job, finished);
                return;
            }

            _world.Reservations.ReleaseItem(sourceItemId);
            _logger.Log($"[TRANS-JOBS][{tick}] Split stack old={sourceItemId} new={newId} take={job.Quantity}");
            job.ItemId = newId;
        }

        _stockpileIndexEmitter.RecordPickup(job.ItemId, currentItemPos);
        _diffEmitter.MarkCarried(job.ItemId, job.CreatureId, workerPosition);
        var toDest = new PathRequest(workerPosition, job.Dest, MoveMode.Walk, PathFlags.AllowDiagonal, _seedFrom(job.CreatureId, job.ItemId));
        IWorldNavigationView view = _navView;
        var path = _paths.Solve(in toDest, in view);
        if (path.Kind == PathResultKind.Found)
        {
            _move.BeginMovement(entityKey, toDest, path);
            job.Stage = JobStage.ToDest;
            _logger.Log($"[TRANS-JOBS][{tick}] Picked item={job.ItemId}; now moving to dest=({job.Dest.X},{job.Dest.Y},{job.Dest.Z})");
            return;
        }

        _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={job.ItemId} no path to dest=({job.Dest.X},{job.Dest.Y},{job.Dest.Z}), unmarking carried");
        _diffEmitter.UnmarkCarried(job.ItemId, workerPosition);
        _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
        _jobFinalizer.Finish(job, finished);
        JobStats.NoPath++;
    }
}
