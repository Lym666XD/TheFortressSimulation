using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningAssignmentHandler : IMiningAssignmentHandler
{
    private readonly WorldModel _world;
    private readonly IPathService _paths;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly IMiningWorkCostResolver _workCostResolver;
    private readonly MiningTileReservationTracker _reservedTiles;
    private readonly IMiningWorkerCandidateSource? _workerCandidates;
    private readonly IMiningJobLogger _logger;
    private readonly string _systemId;
    private readonly string _jobTag;
    private readonly int _creatureReserveTtlTicks;

    internal MiningAssignmentHandler(
        WorldModel world,
        IPathService paths,
        IWorldNavigationView navView,
        IMovementExecutor move,
        IMiningWorkCostResolver workCostResolver,
        MiningTileReservationTracker reservedTiles,
        IMiningWorkerCandidateSource? workerCandidates,
        IMiningJobLogger? logger,
        string systemId,
        string jobTag,
        int creatureReserveTtlTicks)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));
        _workCostResolver = workCostResolver ?? throw new ArgumentNullException(nameof(workCostResolver));
        _reservedTiles = reservedTiles ?? throw new ArgumentNullException(nameof(reservedTiles));
        _workerCandidates = workerCandidates;
        _logger = logger ?? NullMiningJobLogger.Instance;
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
        _jobTag = jobTag ?? throw new ArgumentNullException(nameof(jobTag));
        _creatureReserveTtlTicks = creatureReserveTtlTicks;
    }

    internal ActiveMiningJob? TryAssign(
        in MiningSystem.PlannedDig dig,
        Point adjacent,
        IReadOnlyList<CreatureInstance> creatures,
        HashSet<Guid> busy,
        ulong tick,
        bool middleAlreadySatisfied)
    {
        var jobPoint = new Point3(dig.Cell.X, dig.Cell.Y, dig.Z);
        var candidates = _workerCandidates?.SelectCandidates(_world, _jobTag, busy, _world.Reservations, tick, jobPoint)
            ?? creatures;

        foreach (var worker in candidates)
        {
            if (worker.HP <= 0)
            {
                continue;
            }

            if (busy.Contains(worker.Guid))
            {
                continue;
            }

            if (!_world.Reservations.TryReserveCreature(worker.Guid, _systemId, tick, tick + (ulong)_creatureReserveTtlTicks, jobId: $"mine:{dig.DesignationId}"))
            {
                continue;
            }

            var request = new PathRequest(
                new Point3(worker.Position.X, worker.Position.Y, worker.Z),
                new Point3(adjacent.X, adjacent.Y, dig.Z),
                MoveMode.Walk,
                PathFlags.AllowDiagonal,
                MiningPathSeed.From(worker.Guid, dig.Cell));
            var path = _paths.Solve(in request, in _navView);
            if (path.Kind != PathResultKind.Found)
            {
                _world.Reservations.ReleaseCreature(worker.Guid);
                continue;
            }

            int requiredTicks = _workCostResolver.CalculateRequiredTicks(dig.GeologyHandle, (TerrainKind)dig.TerrainKind);
            if (dig.Action == MiningAction.DigStairwell && dig.Segment == MiningSegment.Middle && middleAlreadySatisfied)
            {
                requiredTicks = Math.Min(requiredTicks, 1);
            }

            var job = new ActiveMiningJob
            {
                WorkerId = worker.Guid,
                Target = dig.Cell,
                Z = dig.Z,
                Adjacent = adjacent,
                Stage = MiningStage.ToAdj,
                ProgressTicks = 0,
                RequiredTicks = requiredTicks,
                GeologyHandle = dig.GeologyHandle,
                TerrainKind = (TerrainKind)dig.TerrainKind,
                Priority = dig.Priority,
                AssignedTick = tick,
                ReplanFailCount = 0,
                Action = dig.Action,
                Segment = dig.Segment,
                DesignationId = dig.DesignationId
            };

            _move.BeginMovement(DiffTargetEncoding.EntityKey(worker.Guid), request, path);
            busy.Add(worker.Guid);
            _reservedTiles.Reserve(dig);
            _logger.Log($"[MINING][{tick}] Assign worker={worker.Guid} target=({dig.Cell.X},{dig.Cell.Y},{dig.Z}) id={dig.DesignationId} adj=({adjacent.X},{adjacent.Y},{dig.Z}) terrain={job.TerrainKind} ticks={requiredTicks}");
            return job;
        }

        return null;
    }

    ActiveMiningJob? IMiningAssignmentHandler.TryAssign(
        in MiningSystem.PlannedDig dig,
        Point adjacent,
        IReadOnlyList<CreatureInstance> creatures,
        HashSet<Guid> busy,
        ulong tick,
        bool middleAlreadySatisfied) =>
        TryAssign(in dig, adjacent, creatures, busy, tick, middleAlreadySatisfied);
}
