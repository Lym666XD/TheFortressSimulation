using HumanFortress.Contracts.Navigation;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Configuration;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Diff;
using HumanFortress.Jobs.Orchestration;
using HumanFortress.Jobs.Profession;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Jobs;

/// <summary>
/// Tick-facing composition shell for the Jobs-owned craft executor.
/// </summary>
internal sealed class CraftJobSystem : IUnifiedCraftJobExecutor
{
    private readonly CraftJobExecutor _executor;
    private readonly IPathService _paths;

    internal CraftJobSystem(
        World world,
        CraftPlanner planner,
        ICraftRecipeCatalog recipes,
        IConstructionCatalog constructions,
        ItemsDiffLog itemsDiffLog,
        NavigationManager? sharedNav,
        ProfessionAssignments? professions,
        WorkerSelectionStrategy workerStrategy,
        NavigationTuning? navigationTuning = null,
        RuntimeNavigationServices? navigationServices = null,
        StockpileDiffLog? stockpileDiffLog = null)
    {
        var tuning = navigationTuning ?? NavigationTuning.Default;
        var navigation = sharedNav ?? SimulationNavigationFactory.Create(world, rebuildAll: true, tuning);
        var jobNavigation = (navigationServices ?? new RuntimeNavigationServices(null, tuning)).CreateJobServices(navigation);
        _paths = jobNavigation.PathService;
        var diffEmitter = new CraftDiffEmitter(itemsDiffLog, Priority, SystemId, world, stockpileDiffLog);
        ICraftWorkerCandidateSource? workerCandidates = professions == null
            ? null
            : new CraftProfessionCandidateSource(professions, workerStrategy);
        _executor = new CraftJobExecutor(
            world,
            planner,
            recipes,
            constructions,
            jobNavigation.PathService,
            jobNavigation.WorldView,
            jobNavigation.Movement,
            diffEmitter,
            workerCandidates);
    }

    internal int LastIntakeCount => _executor.LastIntakeCount;

    internal int Priority => UpdateOrder.Priority.Jobs;

    internal string SystemId => CraftJobExecutor.SystemId;

    internal IPathService PathService => _paths;

    int IUnifiedJobExecutor.LastIntakeCount => LastIntakeCount;

    internal void PrepareSequentialCompatibility(ulong tick) => _executor.PrepareSequentialCompatibility(tick);

    internal void ApplySequentialCompatibility(ulong tick) => _executor.ApplySequentialCompatibility(tick);

    void ISequentialCompatibilityStage.PrepareSequentialCompatibility(ulong tick)
        => PrepareSequentialCompatibility(tick);

    void ISequentialCompatibilityStage.ApplySequentialCompatibility(ulong tick)
        => ApplySequentialCompatibility(tick);

    internal IReadOnlyList<ActiveCraftJobView> GetActiveJobsSnapshot() => _executor.GetActiveJobsSnapshot();

    internal CraftJobReplaySnapshot GetReplaySnapshot() => _executor.GetReplaySnapshot();

    internal CraftJobRestoreResult RestoreReplaySnapshot(CraftJobReplaySnapshot snapshot) => _executor.RestoreReplaySnapshot(snapshot);

    internal CraftJobStatsSnapshot GetLastStatsSnapshot() => _executor.GetLastStatsSnapshot();
}
