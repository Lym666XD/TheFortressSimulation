using HumanFortress.Content.Loading;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Runtime.Session;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Composition;

internal static class FortressRuntimeHostFactory
{
    internal static SimulationRuntimeHost<SimulationRuntimeSystems> Create(
        World world,
        RuntimeSessionServices services,
        NavigationManager navigation,
        string baseDir,
        FortressRuntimeContentSnapshot? content = null,
        FortressRuntimeLogging? logging = null,
        int transportPlanningWorkerCount = 1)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);
        if (transportPlanningWorkerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(transportPlanningWorkerCount));

        logging ??= FortressRuntimeLogging.None;

        var dependencies = FortressRuntimeDependencies.Load(world, baseDir, content, logging.Log);
        var pathServices = new RuntimePathServiceRegistry();

        return new SimulationRuntimeHost<SimulationRuntimeSystems>(
            world,
            services,
            navigation,
            () => FortressRuntimeSystemsFactory.Create(
                world,
                services.DiffLog,
                services.ItemsDiffLog,
                services.MutationDiffs.Stockpiles,
                navigation,
                pathServices,
                dependencies,
                logging,
                transportPlanningWorkerCount),
            (bindings, systems) => bindings.SetProfessionWeightHandler(systems.ProfessionAssignments.SetWeight),
            logging.Log,
            dependencies.Recipes,
            dependencies.Constructions,
            dependencies.Geology,
            dependencies.NavigationTuning,
            dependencies.StockpilePresets,
            pathServices,
            dependencies.WorkshopCategoryTags);
    }

}
