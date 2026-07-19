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
    private readonly TransportStatsTracker _stats;
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
        TransportStatsTracker stats,
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
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        _creatureReserveTtlTicks = creatureReserveTtlTicks;
        _seedFrom = seedFrom ?? throw new ArgumentNullException(nameof(seedFrom));
    }

    internal void HandleArrivedAtItem(ActiveJob job, ulong tick, ulong entityKey, Point3 workerPosition, ICollection<ActiveJob> finished)
    {
        if (job.PendingSplitReservation.IsValid)
        {
            Guid splitItemId = job.PendingSplitReservation.ResourceId;
            if (_world.Items.GetInstance(splitItemId) == null)
            {
                if (tick <= job.PendingSplitIssuedTick)
                    return;

                _world.Reservations.TryCancelStagedItemTransfer(job.PendingSplitReservation);
                _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={job.ItemId} because staged split {splitItemId} was not committed");
                _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
                _jobFinalizer.Finish(job, finished);
                return;
            }

            if (!_world.Reservations.TryCommitStagedItemTransfer(
                    job.ItemReservation,
                    job.PendingSplitReservation,
                    tick,
                    tick + (ulong)_creatureReserveTtlTicks))
            {
                _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={job.ItemId} because staged split reservation lost ownership");
                _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
                _jobFinalizer.Finish(job, finished);
                return;
            }

            var sourceItemId = job.ItemId;
            job.ItemId = splitItemId;
            job.ItemReservation = job.PendingSplitReservation;
            job.PendingSplitReservation = default;
            job.PendingSplitIssuedTick = 0;
            _logger.Log($"[TRANS-JOBS][{tick}] Committed split reservation old={sourceItemId} new={splitItemId}");
        }

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
            job.PathSearchAttempt = 0;
            var retry = new PathRequest(workerPosition, currentItemPos, MoveMode.Walk, PathFlags.AllowDiagonal, _seedFrom(job.CreatureId, job.ItemId));
            IWorldNavigationView retryView = _navView;
            var retryPath = _paths.Solve(in retry, in retryView);
            if (retryPath.Kind == PathResultKind.Found)
            {
                _move.BeginMovement(entityKey, retry, retryPath);
                _logger.Log($"[TRANS-JOBS][{tick}] Repath to moved item={job.ItemId} from=({workerPosition.X},{workerPosition.Y},{workerPosition.Z}) to=({currentItemPos.X},{currentItemPos.Y},{currentItemPos.Z})");
                return;
            }

            if (retryPath.Kind is PathResultKind.Partial or PathResultKind.BudgetExhausted)
            {
                _move.BeginMovement(entityKey, retry, retryPath);
                _logger.Log($"[TRANS-JOBS][{tick}] Defer moved-item repath item={job.ItemId} kind={retryPath.Kind}");
                return;
            }

            _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={job.ItemId} moved to unreachable pickup=({currentItemPos.X},{currentItemPos.Y},{currentItemPos.Z}) kind={retryPath.Kind}");
            _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
            _jobFinalizer.Finish(job, finished);
            _stats.RecordNoPath();
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

            if (!_world.Reservations.TryStageItemTransfer(
                    job.ItemReservation,
                    newId,
                    tick,
                    tick + (ulong)_creatureReserveTtlTicks,
                    out var stagedReservation))
            {
                _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={sourceItemId} split reservation failed new={newId}");
                _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
                _jobFinalizer.Finish(job, finished);
                return;
            }

            if (!_diffEmitter.SplitStack(
                    sourceItemId,
                    newId,
                    inst.Position.X,
                    inst.Position.Y,
                    inst.Z,
                    job.Quantity,
                    job.ItemReservation,
                    stagedReservation))
            {
                _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={sourceItemId} split emit failed take={job.Quantity}");
                _world.Reservations.TryCancelStagedItemTransfer(stagedReservation);
                _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
                _jobFinalizer.Finish(job, finished);
                return;
            }

            job.PendingSplitReservation = stagedReservation;
            job.PendingSplitIssuedTick = tick;
            _logger.Log($"[TRANS-JOBS][{tick}] Staged split old={sourceItemId} new={newId} take={job.Quantity}");
            return;
        }

        _stockpileIndexEmitter.RecordPickup(job.ItemId, currentItemPos);
        _diffEmitter.MarkCarried(job.ItemId, job.CreatureId, workerPosition);
        job.PathSearchAttempt = 0;
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

        if (path.Kind is PathResultKind.Partial or PathResultKind.BudgetExhausted)
        {
            _move.BeginMovement(entityKey, toDest, path);
            job.Stage = JobStage.ToDest;
            _logger.Log($"[TRANS-JOBS][{tick}] Picked item={job.ItemId}; defer destination path kind={path.Kind}");
            return;
        }

        _logger.Log($"[TRANS-JOBS][{tick}] Drop job item={job.ItemId} no path to dest=({job.Dest.X},{job.Dest.Y},{job.Dest.Z}), unmarking carried");
        _diffEmitter.UnmarkCarried(job.ItemId, workerPosition);
        _stockpileIndexEmitter.ReleaseDestinationReservation(job.Dest, job.Reason);
        _jobFinalizer.Finish(job, finished);
        _stats.RecordNoPath();
    }
}
