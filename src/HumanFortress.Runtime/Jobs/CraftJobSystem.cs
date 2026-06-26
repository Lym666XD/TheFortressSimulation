using HumanFortress.Contracts.Navigation;
using HumanFortress.Jobs;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Jobs.Craft;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Jobs;

/// <summary>
/// Tick-facing composition shell for the Jobs-owned craft executor.
/// </summary>
internal sealed class CraftJobSystem : ITick, IUnifiedCraftJobExecutor
{
    private readonly CraftJobExecutor _executor;

    internal CraftJobSystem(
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
        var move = new MovementExecutor(paths);
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
            move,
            diffEmitter,
            workerCandidates);
    }

    internal int LastIntakeCount => _executor.LastIntakeCount;

    internal int Priority => UpdateOrder.Priority.Jobs;

    internal string SystemId => CraftJobExecutor.SystemId;

    int IUnifiedJobExecutor.LastIntakeCount => LastIntakeCount;

    int ITick.Priority => Priority;

    string ITick.SystemId => SystemId;

    void ITick.ReadTick(ulong tick) => ReadTick(tick);

    void ITick.WriteTick(ulong tick) => WriteTick(tick);

    internal void ReadTick(ulong tick) => _executor.ReadTick(tick);

    internal void WriteTick(ulong tick) => _executor.WriteTick(tick);

    internal IReadOnlyList<ActiveCraftJobView> GetActiveJobsSnapshot() => _executor.GetActiveJobsSnapshot();

    internal CraftJobStatsSnapshot GetLastStatsSnapshot() => _executor.GetLastStatsSnapshot();
}
