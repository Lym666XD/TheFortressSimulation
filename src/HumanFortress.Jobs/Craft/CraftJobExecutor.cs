using System.Collections.Concurrent;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Navigation;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

internal sealed class CraftJobExecutor
{
    public const string SystemId = "Jobs.Craft";

    private const int CreatureReserveTtlTicks = 200;

    private readonly WorldModel _world;
    private readonly ICraftJobPlanner _planner;
    private readonly CraftJobFinalizer _finalizer;
    private readonly CraftAssignmentHandler _assignmentHandler;
    private readonly CraftActiveJobRunner _activeJobRunner;
    private readonly IPathService _paths;
    private readonly CraftStatsTracker _stats = new();

    private readonly List<PlannedCraftJob> _inbox = new();
    private readonly ConcurrentQueue<PlannedCraftJob> _backlog = new();
    private readonly List<ActiveCraftJob> _active = new();

    public CraftJobExecutor(
        WorldModel world,
        ICraftJobPlanner planner,
        ICraftRecipeCatalog recipes,
        IConstructionCatalog constructions,
        IPathService paths,
        IWorldNavigationView navView,
        ICraftDiffEmitter diffEmitter,
        ICraftWorkerCandidateSource? workerCandidates)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));

        var workshops = new CraftWorkshopLocator(world, constructions);
        var materialConsumer = new CraftMaterialConsumer(world, workshops, recipes, diffEmitter);
        var outputEmitter = new CraftOutputEmitter(recipes, diffEmitter);
        _finalizer = new CraftJobFinalizer(world, workshops);
        var move = new MovementExecutor(paths);
        _assignmentHandler = new CraftAssignmentHandler(
            world,
            workshops,
            paths,
            navView,
            move,
            recipes,
            workerCandidates,
            SystemId,
            CreatureReserveTtlTicks);
        _activeJobRunner = new CraftActiveJobRunner(
            world,
            paths,
            navView,
            move,
            materialConsumer,
            outputEmitter,
            SystemId,
            CreatureReserveTtlTicks);
    }

    public int LastIntakeCount { get; private set; }

    public void ReadTick(ulong tick)
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

    public void WriteTick(ulong tick)
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

    public IReadOnlyList<ActiveCraftJobView> GetActiveJobsSnapshot()
    {
        var list = new List<ActiveCraftJobView>(_active.Count);
        foreach (var job in _active)
        {
            list.Add(new ActiveCraftJobView(job.WorkerId, job.WorkshopGuid, job.RecipeId, job.Stage.ToString(), job.WorkTicksRemaining));
        }

        return list;
    }

    public CraftJobStatsSnapshot GetLastStatsSnapshot() => _stats.Snapshot;
}
