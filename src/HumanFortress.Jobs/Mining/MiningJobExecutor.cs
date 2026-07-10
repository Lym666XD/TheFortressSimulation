using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Mining;

internal sealed partial class MiningJobExecutor
{
    internal const string SystemId = "Jobs.MiningJobSystem";

    private const int CreatureReserveTtlTicks = 200;
    private const string JobTag = "mining";
    private const int MaxFailedReplans = 10;

    private readonly WorldModel _world;
    private readonly IPathService _paths;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly IMiningJobLogger _logger;
    private readonly List<ActiveMiningJob> _active = new();
    private readonly List<MiningSystem.PlannedDig> _inboxBuffer = new();
    private readonly MiningBacklogBuffer _backlog = new();
    private readonly MiningActiveJobRunner _activeJobRunner;
    private readonly MiningIntakeCoordinator _intakeCoordinator;
    private readonly MiningReadJobProcessor _readJobProcessor;
    private readonly MiningStatsTracker _statsTracker;
    private readonly List<(Point Cell, int Z, ulong ExpireTick)> _recentCompleted = new();
    private readonly MiningTileReservationTracker _reservedTiles = new();
    private readonly MiningDeferredStairwellBuffer _deferredStairwells = new();

    internal MiningJobExecutor(
        WorldModel world,
        MiningSystem planner,
        IPathService paths,
        IWorldNavigationView navView,
        IMiningDiffEmitter diffEmitter,
        IMiningDropResolver dropResolver,
        IMiningWorkerCandidateSource? workerCandidates,
        IMiningJobCompletionSink? completionSink,
        IMovementExecutor move,
        IMiningJobLogger? logger,
        int intakeBudget,
        int carryoverMaxTicks)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));
        _logger = logger ?? NullMiningJobLogger.Instance;

        var adjacencyFinder = new MiningAdjacencyFinder(world);
        var resultApplier = new MiningResultApplier(world, diffEmitter, dropResolver);
        var finalizer = new MiningJobFinalizer(_reservedTiles, world.Reservations);
        var stairwellGate = new MiningStairwellGate(world, _deferredStairwells, _logger);
        _intakeCoordinator = new MiningIntakeCoordinator(planner, _backlog, _deferredStairwells, _logger, intakeBudget);
        var assignmentHandler = new MiningAssignmentHandler(
            world,
            paths,
            _navView,
            _move,
            dropResolver,
            _reservedTiles,
            workerCandidates,
            _logger,
            SystemId,
            JobTag,
            CreatureReserveTtlTicks);
        _readJobProcessor = new MiningReadJobProcessor(
            planner,
            _backlog,
            _reservedTiles,
            adjacencyFinder,
            stairwellGate,
            assignmentHandler,
            _logger);
        _activeJobRunner = new MiningActiveJobRunner(
            world,
            planner,
            paths,
            _navView,
            _move,
            _backlog,
            diffEmitter,
            dropResolver,
            resultApplier,
            finalizer,
            completionSink,
            _logger,
            SystemId,
            CreatureReserveTtlTicks,
            MaxFailedReplans);
        _statsTracker = new MiningStatsTracker(Math.Max(1, carryoverMaxTicks));
    }

    internal int LastIntakeCount => _statsTracker.LastIntakeCount;

    internal void ReadTick(ulong tick)
    {
        _paths.BeginTick();
        int intakeCount = _intakeCoordinator.Fill(tick, _inboxBuffer);

        if (intakeCount == 0)
        {
            _statsTracker.RecordIntake(0);
            if ((tick % 60UL) == 0UL)
            {
                _logger.Log($"[MINING][{tick}] No planned digs dequeued.");
            }

            UpdateStats(tick);
            return;
        }

        _statsTracker.RecordIntake(intakeCount);

        var digs = MiningDigOrdering.Sort(_inboxBuffer);
        var creatures = _world.Creatures.GetAllInstances().OrderBy(c => c.Guid).ToList();
        if (digs.Count > 0)
        {
            var idGroups = digs.GroupBy(d => d.DesignationId).Select(g => $"{g.Key}:{g.Count()}").ToArray();
            var idsStr = string.Join(",", idGroups);
            _logger.Log($"[MINING][{tick}] Planned digs dequeued: {digs.Count}; available workers: {creatures.Count}; ids=[{idsStr}]");
        }

        var busy = new HashSet<Guid>(_active.Select(a => a.WorkerId));
        foreach (var pd in digs)
        {
            _readJobProcessor.Process(pd, creatures, busy, _active, tick);
        }

        UpdateStats(tick);
    }

    internal void WriteTick(ulong tick)
    {
        if (_active.Count == 0)
        {
            UpdateStats(tick);
            return;
        }

        _activeJobRunner.RunWriteTick(_active, tick);
        UpdateStats(tick);
    }

    internal List<(Point Cell, int Z)> GetRecentCompletions(ulong now)
    {
        for (int i = _recentCompleted.Count - 1; i >= 0; i--)
        {
            if (_recentCompleted[i].ExpireTick <= now)
            {
                _recentCompleted.RemoveAt(i);
            }
        }

        return _recentCompleted.Select(rc => (rc.Cell, rc.Z)).ToList();
    }

    internal List<MiningActiveJobView> GetActiveJobsSnapshot()
    {
        return MiningDebugSnapshotBuilder.BuildActiveJobs(_active);
    }

    internal MiningDebugSnapshot GetDebugSnapshot(int maxActive = 8, bool includeSeeds = false)
    {
        return MiningDebugSnapshotBuilder.BuildDebugSnapshot(
            _active,
            GetLastStatsSnapshot(),
            _backlog.Count,
            _deferredStairwells.Count,
            _reservedTiles.Count,
            maxActive,
            includeSeeds);
    }

    internal MiningJobReplaySnapshot GetReplaySnapshot()
    {
        var active = new MiningActiveJobStateSnapshot[_active.Count];
        for (var i = 0; i < _active.Count; i++)
        {
            var job = _active[i];
            active[i] = new MiningActiveJobStateSnapshot(
                i,
                job.WorkerId,
                job.Target,
                job.Z,
                job.Adjacent,
                job.Stage,
                job.ProgressTicks,
                job.RequiredTicks,
                job.GeologyHandle,
                job.TerrainKind,
                job.Priority,
                job.AssignedTick,
                job.ReplanFailCount,
                job.Action,
                job.Segment,
                job.DesignationId);
        }

        var recent = new MiningRecentCompletionSnapshot[_recentCompleted.Count];
        for (var i = 0; i < _recentCompleted.Count; i++)
        {
            var completion = _recentCompleted[i];
            recent[i] = new MiningRecentCompletionSnapshot(
                i,
                completion.Cell,
                completion.Z,
                completion.ExpireTick);
        }

        return new MiningJobReplaySnapshot(
            active,
            _backlog.GetStateSnapshot(),
            _deferredStairwells.GetStateSnapshot(),
            _reservedTiles.GetStateSnapshot(),
            recent);
    }

    internal int GetBacklogCount() => _backlog.Count;

    internal int GetDeferredCount() => _deferredStairwells.Count;

    internal int GetReservedTileCount() => _reservedTiles.Count;

    internal MiningJobStatsSnapshot GetLastStatsSnapshot() => _statsTracker.GetSnapshot();

    private void UpdateStats(ulong tick)
    {
        _statsTracker.Update(tick, _active.Count, _backlog, _deferredStairwells.Count, _reservedTiles.Count);
    }
}
