using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Mining;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.App.Jobs;

/// <summary>
/// App-owned composition shell for the Jobs-owned mining executor.
/// </summary>
public sealed class MiningJobSystem : ITick
{
    private readonly NavigationManager _nav;
    private readonly MiningJobExecutor _executor;

    public MiningJobSystem(
        HumanFortress.Simulation.World.World world,
        MiningSystem planner,
        DiffLog? diffLog = null,
        HumanFortress.Simulation.Items.ItemsDiffLog? itemsDiff = null,
        NavigationManager? sharedNav = null,
        int intakeBudget = 16,
        int carryoverMaxTicks = 8,
        ProfessionAssignments? professions = null,
        WorkerSelectionStrategy workerStrategy = WorkerSelectionStrategy.Closest,
        NavigationTuning? navigationTuning = null)
    {
        var tuning = navigationTuning ?? NavigationTuning.Default;
        _nav = sharedNav ?? SimulationNavigationFactory.Create(world, rebuildAll: true, tuning);
        var paths = new PathService(tuning);
        var navView = new WorldNavigationView(_nav);
        var logger = AppMiningJobLogger.Instance;
        var dropResolver = new MiningDropResolver();
        var diffEmitter = new MiningDiffEmitter(diffLog, itemsDiff, SystemId, Priority);
        IMiningWorkerCandidateSource? workerCandidates = professions == null
            ? null
            : new MiningProfessionCandidateSource(professions, workerStrategy);
        IMiningJobCompletionSink? completionSink = professions == null
            ? null
            : new MiningProfessionCompletionSink(professions, "mining");

        _executor = new MiningJobExecutor(
            world,
            planner,
            paths,
            navView,
            diffEmitter,
            dropResolver,
            workerCandidates,
            completionSink,
            logger,
            intakeBudget,
            carryoverMaxTicks);
    }

    public int LastIntakeCount => _executor.LastIntakeCount;

    public int Priority => UpdateOrder.Priority.Jobs;

    public string SystemId => MiningJobExecutor.SystemId;

    public NavigationManager NavigationManager => _nav;

    public void ReadTick(ulong tick) => _executor.ReadTick(tick);

    public void WriteTick(ulong tick) => _executor.WriteTick(tick);

    public List<(Point Cell, int Z)> GetRecentCompletions(ulong now) => _executor.GetRecentCompletions(now);

    public List<MiningActiveJobView> GetActiveJobsSnapshot() => _executor.GetActiveJobsSnapshot();

    public MiningDebugSnapshot GetDebugSnapshot(int maxActive = 8, bool includeSeeds = false)
        => _executor.GetDebugSnapshot(maxActive, includeSeeds);

    public int GetBacklogCount() => _executor.GetBacklogCount();

    public int GetDeferredCount() => _executor.GetDeferredCount();

    public int GetReservedTileCount() => _executor.GetReservedTileCount();

    public MiningJobStatsSnapshot GetLastStatsSnapshot() => _executor.GetLastStatsSnapshot();
}
