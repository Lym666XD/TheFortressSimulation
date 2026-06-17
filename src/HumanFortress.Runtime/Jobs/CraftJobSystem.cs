using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Craft;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Jobs;

/// <summary>
/// Tick-facing composition shell for the Jobs-owned craft executor.
/// </summary>
public sealed class CraftJobSystem : ITick, IUnifiedCraftJobExecutor
{
    private readonly CraftJobExecutor _executor;

    public CraftJobSystem(
        World world,
        CraftPlanner planner,
        ICraftRecipeCatalog recipes,
        IConstructionCatalog constructions,
        ItemsDiffLog itemsDiffLog,
        NavigationManager? sharedNav,
        ProfessionAssignments? professions,
        WorkerSelectionStrategy workerStrategy,
        NavigationTuning? navigationTuning = null)
    {
        var tuning = navigationTuning ?? NavigationTuning.Default;
        var navigation = sharedNav ?? SimulationNavigationFactory.Create(world, rebuildAll: true, tuning);
        var paths = new PathService(tuning);
        var navView = new WorldNavigationView(navigation);
        IWorldNavigationView navViewInterface = navView;
        var diffEmitter = new CraftDiffEmitter(itemsDiffLog, Priority, SystemId);
        ICraftWorkerCandidateSource? workerCandidates = professions == null
            ? null
            : new CraftProfessionCandidateSource(professions, workerStrategy);
        _executor = new CraftJobExecutor(
            world,
            planner,
            recipes,
            constructions,
            paths,
            navViewInterface,
            diffEmitter,
            workerCandidates);
    }

    public int LastIntakeCount => _executor.LastIntakeCount;

    public int Priority => UpdateOrder.Priority.Jobs;

    public string SystemId => CraftJobExecutor.SystemId;

    public void ReadTick(ulong tick) => _executor.ReadTick(tick);

    public void WriteTick(ulong tick) => _executor.WriteTick(tick);

    public IReadOnlyList<ActiveCraftJobView> GetActiveJobsSnapshot() => _executor.GetActiveJobsSnapshot();

    public CraftJobStatsSnapshot GetLastStatsSnapshot() => _executor.GetLastStatsSnapshot();
}
