using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningActiveJobRunner
{
    private readonly WorldModel _world;
    private readonly MiningSystem _planner;
    private readonly IPathService _paths;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly MiningBacklogBuffer _backlog;
    private readonly IMiningDiffEmitter _diffEmitter;
    private readonly IMiningDropResolver _dropResolver;
    private readonly MiningResultApplier _resultApplier;
    private readonly MiningJobFinalizer _finalizer;
    private readonly IMiningJobCompletionSink? _completionSink;
    private readonly IMiningJobLogger _logger;
    private readonly int _creatureReserveTtlTicks;
    private readonly int _maxFailedReplans;

    internal MiningActiveJobRunner(
        WorldModel world,
        MiningSystem planner,
        IPathService paths,
        IWorldNavigationView navView,
        IMovementExecutor move,
        MiningBacklogBuffer backlog,
        IMiningDiffEmitter diffEmitter,
        IMiningDropResolver dropResolver,
        MiningResultApplier resultApplier,
        MiningJobFinalizer finalizer,
        IMiningJobCompletionSink? completionSink,
        IMiningJobLogger? logger,
        int creatureReserveTtlTicks,
        int maxFailedReplans)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));
        _backlog = backlog ?? throw new ArgumentNullException(nameof(backlog));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
        _dropResolver = dropResolver ?? throw new ArgumentNullException(nameof(dropResolver));
        _resultApplier = resultApplier ?? throw new ArgumentNullException(nameof(resultApplier));
        _finalizer = finalizer ?? throw new ArgumentNullException(nameof(finalizer));
        _completionSink = completionSink;
        _logger = logger ?? NullMiningJobLogger.Instance;
        _creatureReserveTtlTicks = creatureReserveTtlTicks;
        _maxFailedReplans = maxFailedReplans;
    }

    internal void RunWriteTick(List<ActiveMiningJob> active, ulong tick)
    {
        var finished = new List<ActiveMiningJob>();
        foreach (var job in active)
        {
            if (_planner.IsTileCanceled(job.Target.X, job.Target.Y, job.Z))
            {
                _logger.Log($"[MINING][{tick}] Cancel job at target=({job.Target.X},{job.Target.Y},{job.Z}) id={job.DesignationId} seg={job.Segment}; release worker={job.WorkerId}");
                _finalizer.Finish(job, finished);
                continue;
            }

            var worker = _world.Creatures.GetInstance(job.WorkerId);
            if (worker == null)
            {
                Requeue(job, tick);
                _logger.Log($"[MINING][{tick}] Worker missing; release & requeue target=({job.Target.X},{job.Target.Y},{job.Z}) id={job.DesignationId} seg={job.Segment}");
                _finalizer.Finish(job, finished);
                continue;
            }

            ulong entityKey = DiffTargetEncoding.EntityKey(worker.Guid);
            if (!_world.Reservations.TryRenewCreature(
                    job.CreatureReservation,
                    tick,
                    tick + (ulong)_creatureReserveTtlTicks))
            {
                Requeue(job, tick);
                _finalizer.Finish(job, finished);
                continue;
            }

            if (job.Stage == MiningStage.ToAdj)
            {
                HandleMoveToAdj(job, entityKey, tick, finished);
                continue;
            }

            if (job.Stage == MiningStage.Digging)
            {
                HandleDigging(job, tick, finished);
            }
        }

        if (finished.Count > 0)
        {
            foreach (var job in finished)
            {
                active.Remove(job);
            }
        }
    }

    private void HandleMoveToAdj(ActiveMiningJob job, ulong entityKey, ulong tick, List<ActiveMiningJob> finished)
    {
        var update = _move.UpdateMovement(entityKey, _navView);
        if (update.NeedsReplan)
        {
            job.PathSearchAttempt = update.SearchAttempt;
            var req = new PathRequest(
                update.Position,
                new Point3(job.Adjacent.X, job.Adjacent.Y, job.Z),
                MoveMode.Walk,
                PathFlags.AllowDiagonal,
                MiningPathSeed.From(job.WorkerId, job.Target),
                job.PathSearchAttempt);
            var path = _paths.Solve(in req, in _navView);
            if (path.Kind == PathResultKind.Found)
            {
                _move.BeginMovement(entityKey, req, path);
                job.ReplanFailCount = 0;
                _logger.Log($"[MINING][{tick}] Replan worker={job.WorkerId} to adj=({job.Adjacent.X},{job.Adjacent.Y},{job.Z}) kind={path.Kind} id={job.DesignationId}");
            }
            else if (path.Kind is PathResultKind.Partial or PathResultKind.BudgetExhausted)
            {
                _move.BeginMovement(entityKey, req, path);
                _logger.Log($"[MINING][{tick}] Replan deferred kind={path.Kind} worker={job.WorkerId} to adj=({job.Adjacent.X},{job.Adjacent.Y},{job.Z}) id={job.DesignationId}");
            }
            else
            {
                job.ReplanFailCount++;
                _logger.Log($"[MINING][{tick}] Replan failed kind={path.Kind} worker={job.WorkerId} to adj=({job.Adjacent.X},{job.Adjacent.Y},{job.Z}) fails={job.ReplanFailCount} id={job.DesignationId}");
                if (job.ReplanFailCount >= _maxFailedReplans)
                {
                    if (!_planner.IsTileCanceled(job.Target.X, job.Target.Y, job.Z))
                    {
                        Requeue(job, tick);
                    }

                    _logger.Log($"[MINING][{tick}] Release reservation & requeue target=({job.Target.X},{job.Target.Y},{job.Z}) id=0(backlog) seg={job.Segment} due to timeout (path={path.Kind})");
                    _finalizer.Finish(job, finished);
                }
            }

            return;
        }

        _diffEmitter.MoveCreature(job.WorkerId, update.Position);

        if (update.Status == MovementStatus.Arrived)
        {
            job.PathSearchAttempt = 0;
            job.Stage = MiningStage.Digging;
            _logger.Log($"[MINING][{tick}] Start digging by worker={job.WorkerId} at target=({job.Target.X},{job.Target.Y},{job.Z}) id={job.DesignationId} seg={job.Segment}");
            EmitStairwellPreOpen(job);
        }
    }

    private void HandleDigging(ActiveMiningJob job, ulong tick, List<ActiveMiningJob> finished)
    {
        job.ProgressTicks++;
        if (job.ProgressTicks < job.RequiredTicks)
        {
            return;
        }

        var verifyTile = _world.GetTile(job.Target.X, job.Target.Y, job.Z);
        if (verifyTile != null)
        {
            if (job.Action == MiningAction.DigChannel && AnyCreatureAtExcept(job.Target, job.Z, job.WorkerId))
            {
                Requeue(job, tick);
                _logger.Log($"[MINING][{tick}] Channel target occupied by other creature at ({job.Target.X},{job.Target.Y},{job.Z}) id=0(backlog); requeue");
                _finalizer.Finish(job, finished);
                return;
            }

            job.Stage = MiningStage.Complete;
            _resultApplier.Apply(job);
            _logger.Log($"[MINING][{tick}] Dig complete at target=({job.Target.X},{job.Target.Y},{job.Z}) action={job.Action} id={job.DesignationId} seg={job.Segment} by {job.WorkerId}");
            _completionSink?.RecordJobCompletion(job.WorkerId, "mining");
        }
        else
        {
            _logger.Log($"[MINING][{tick}] Tile ({job.Target.X},{job.Target.Y},{job.Z}) id={job.DesignationId} changed during dig, aborting");
        }

        _finalizer.Finish(job, finished);
    }

    private void EmitStairwellPreOpen(ActiveMiningJob job)
    {
        if (job.Action != MiningAction.DigStairwell || job.Z <= 0)
        {
            return;
        }

        if (job.Segment == MiningSegment.Bottom)
        {
            return;
        }

        int belowZ = job.Z - 1;
        var below = _world.GetTile(job.Target.X, job.Target.Y, belowZ);
        if (below == null || below.Value.Kind != TerrainKind.SolidWall)
        {
            return;
        }

        _diffEmitter.SetTerrain(job.Target, belowZ, TerrainKind.StairsUD, below.Value.GeoMatId);
        foreach (var (dropId, qty) in _dropResolver.ChooseDropsFor(below.Value.GeoMatId, TerrainKind.SolidWall))
        {
            if (!string.IsNullOrEmpty(dropId) && qty > 0)
            {
                _diffEmitter.AddItem(job.Target, belowZ, dropId, qty);
            }
        }

        _logger.Log($"[MINING] Stair Pre-open UD at ({job.Target.X},{job.Target.Y},{belowZ}) (one layer)");
    }

    private void Requeue(ActiveMiningJob job, ulong tick)
    {
        _backlog.Enqueue(new MiningSystem.PlannedDig(job.Target, job.Z, job.GeologyHandle, (byte)job.TerrainKind, job.Priority, 0UL, job.Action, job.Segment, job.DesignationId), tick);
    }

    private bool AnyCreatureAtExcept(Point cell, int z, Guid exceptId)
    {
        foreach (var creature in _world.Creatures.GetAllInstances())
        {
            if (creature.Z == z && creature.Position.X == cell.X && creature.Position.Y == cell.Y && creature.Guid != exceptId)
            {
                return true;
            }
        }

        return false;
    }
}
