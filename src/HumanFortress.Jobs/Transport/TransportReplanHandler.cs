using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.World;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Transport;

internal sealed class TransportReplanHandler
{
    private readonly WorldModel _world;
    private readonly IPathService _paths;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly ITransportMovementDiffEmitter _diffEmitter;
    private readonly ITransportJobLogger _logger;
    private readonly Func<Guid, Guid, uint> _seedFrom;

    internal TransportReplanHandler(
        WorldModel world,
        IPathService paths,
        IWorldNavigationView navView,
        IMovementExecutor move,
        ITransportMovementDiffEmitter diffEmitter,
        ITransportJobLogger? logger,
        Func<Guid, Guid, uint> seedFrom)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
        _logger = logger ?? NullTransportJobLogger.Instance;
        _seedFrom = seedFrom ?? throw new ArgumentNullException(nameof(seedFrom));
    }

    internal void HandleReplan(ActiveJob job, ulong tick, uint entityId, MovementUpdate update, Point3 goal)
    {
        var source = update.Position;
        var request = new PathRequest(source, goal, MoveMode.Walk, PathFlags.AllowDiagonal, _seedFrom(job.CreatureId, job.ItemId));
        IWorldNavigationView view = _navView;
        var path = _paths.Solve(in request, in view);
        if (path.Kind == PathResultKind.Found)
        {
            _move.BeginMovement(entityId, request, path);
        }
        else if (path.Kind == PathResultKind.Invalid)
        {
            TryUnstuckAfterInvalidReplan(job, tick, entityId, source, goal, view);
        }

        _logger.Log($"[TRANS-JOBS][{tick}] Replan worker={job.CreatureId} stage={job.Stage} from=({source.X},{source.Y},{source.Z}) goal=({goal.X},{goal.Y},{goal.Z}) kind={path.Kind}");
    }

    private void TryUnstuckAfterInvalidReplan(ActiveJob job, ulong tick, uint entityId, Point3 source, Point3 goal, IWorldNavigationView view)
    {
        job.InvalidReplanCount++;
        if (!IsBadSourceCell(source)) return;
        if (job.InvalidReplanCount < 2) return;

        var safe = WorldSafetyQueries.FindNearestStandableNonConstructionSite(_world, source.X, source.Y, source.Z, 3);
        if (safe == null) return;

        var safePoint = new Point3(safe.Value.X, safe.Value.Y, safe.Value.Z);
        _diffEmitter.MoveCreature(entityId, safePoint);
        job.InvalidReplanCount = 0;

        var request = new PathRequest(safePoint, goal, MoveMode.Walk, PathFlags.AllowDiagonal, _seedFrom(job.CreatureId, job.ItemId));
        var path = _paths.Solve(in request, in view);
        if (path.Kind == PathResultKind.Found)
        {
            _move.BeginMovement(entityId, request, path);
        }

        _logger.Log($"[TRANS-JOBS][{tick}] UNSTUCK worker={job.CreatureId} from=({source.X},{source.Y},{source.Z}) to=({safePoint.X},{safePoint.Y},{safePoint.Z}) kind={path.Kind}");
    }

    private bool IsBadSourceCell(Point3 source)
    {
        var tile = _world.GetTile(source.X, source.Y, source.Z);
        return tile == null || !(tile.Value.IsStandable || tile.Value.IsWalkable);
    }
}
