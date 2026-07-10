using HumanFortress.Contracts.Navigation;
using HumanFortress.Contracts.Content.Registry;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

internal sealed partial class CraftJobExecutor
{
    internal const string SystemId = "Jobs.Craft";

    private const int CreatureReserveTtlTicks = 200;

    private readonly WorldModel _world;
    private readonly ICraftJobPlanner _planner;
    private readonly ICraftRecipeCatalog _recipes;
    private readonly CraftWorkshopLocator _workshops;
    private readonly CraftJobFinalizer _finalizer;
    private readonly CraftAssignmentHandler _assignmentHandler;
    private readonly CraftActiveJobRunner _activeJobRunner;
    private readonly IPathService _paths;
    private readonly IWorldNavigationView _navView;
    private readonly IMovementExecutor _move;
    private readonly CraftStatsTracker _stats = new();

    private readonly List<PlannedCraftJob> _inbox = new();
    private readonly Queue<PlannedCraftJob> _backlog = new();
    private readonly List<ActiveCraftJob> _active = new();

    internal CraftJobExecutor(
        WorldModel world,
        ICraftJobPlanner planner,
        ICraftRecipeCatalog recipes,
        IConstructionCatalog constructions,
        IPathService paths,
        IWorldNavigationView navView,
        IMovementExecutor move,
        ICraftDiffEmitter diffEmitter,
        ICraftWorkerCandidateSource? workerCandidates)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _navView = navView ?? throw new ArgumentNullException(nameof(navView));
        _move = move ?? throw new ArgumentNullException(nameof(move));

        _workshops = new CraftWorkshopLocator(world, constructions);
        var materialConsumer = new CraftMaterialConsumer(world, _workshops, _recipes, diffEmitter);
        var outputEmitter = new CraftOutputEmitter(_recipes, diffEmitter);
        _finalizer = new CraftJobFinalizer(world, _workshops);
        _assignmentHandler = new CraftAssignmentHandler(
            world,
            _workshops,
            paths,
            _navView,
            _move,
            _recipes,
            workerCandidates,
            SystemId,
            CreatureReserveTtlTicks);
        _activeJobRunner = new CraftActiveJobRunner(
            world,
            paths,
            _navView,
            _move,
            materialConsumer,
            outputEmitter,
            SystemId,
            CreatureReserveTtlTicks);
    }

    internal int LastIntakeCount { get; private set; }

    internal void ReadTick(ulong tick)
    {
        _paths.BeginTick();
        _inbox.Clear();

        while (_backlog.TryDequeue(out var pending))
        {
            _inbox.Add(pending);
        }

        _planner.DequeuePlannedJobs(32, _inbox);
        LastIntakeCount = _inbox.Count;
        if (_inbox.Count == 0)
        {
            _stats.RecordRead(0, _active.Count, _backlog.Count);
            return;
        }

        var workers = _world.Creatures.GetAllInstances().OrderBy(c => c.Guid).ToList();
        var busy = new HashSet<Guid>(_active.Select(j => j.WorkerId));

        foreach (var job in _inbox)
        {
            var result = _assignmentHandler.TryAssign(job, workers, busy, tick, out var activeJob);
            if (result == CraftAssignmentResult.Assigned && activeJob != null)
            {
                _active.Add(activeJob);
                continue;
            }

            if (result == CraftAssignmentResult.Backlog)
            {
                _backlog.Enqueue(job);
            }
        }

        _stats.RecordRead(LastIntakeCount, _active.Count, _backlog.Count);
    }

    internal void WriteTick(ulong tick)
    {
        if (_active.Count == 0)
        {
            return;
        }

        var finished = new List<(ActiveCraftJob Job, CraftJobFinishReason Reason)>();
        foreach (var job in _active)
        {
            if (_activeJobRunner.Run(job, tick, out var reason))
            {
                finished.Add((job, reason));
                if (reason == CraftJobFinishReason.Completed)
                {
                    _stats.RecordCompleted();
                }
            }
        }

        foreach (var (job, reason) in finished)
        {
            _finalizer.Finish(job, reason);
            _active.Remove(job);
        }

        _stats.RecordWrite(LastIntakeCount, _active.Count, _backlog.Count);
    }

    internal IReadOnlyList<ActiveCraftJobView> GetActiveJobsSnapshot()
    {
        var list = new List<ActiveCraftJobView>(_active.Count);
        foreach (var job in _active)
        {
            list.Add(new ActiveCraftJobView(job.WorkerId, job.WorkshopGuid, job.RecipeId, job.Stage.ToString(), job.WorkTicksRemaining));
        }

        return list;
    }

    internal CraftJobReplaySnapshot GetReplaySnapshot()
    {
        var active = new CraftActiveJobStateSnapshot[_active.Count];
        for (var i = 0; i < _active.Count; i++)
        {
            var job = _active[i];
            active[i] = new CraftActiveJobStateSnapshot(
                i,
                job.WorkerId,
                job.WorkshopGuid,
                job.QueueEntryId,
                job.RecipeId,
                job.Stage,
                job.WorkTicksRemaining,
                job.Anchor,
                job.Z);
        }

        var backlogJobs = _backlog.ToArray();
        var backlog = new CraftBacklogEntrySnapshot[backlogJobs.Length];
        for (var i = 0; i < backlogJobs.Length; i++)
        {
            backlog[i] = new CraftBacklogEntrySnapshot(i, backlogJobs[i]);
        }

        return new CraftJobReplaySnapshot(active, backlog);
    }

    internal CraftJobStatsSnapshot GetLastStatsSnapshot() => _stats.Snapshot;
}
