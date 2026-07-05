using HumanFortress.Content.Loading;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Host;
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
        FortressRuntimeLogging? logging = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        logging ??= FortressRuntimeLogging.None;

        var dependencies = FortressRuntimeDependencies.Load(world, baseDir, content, logging.Log);

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
                dependencies,
                logging),
            (bindings, systems) => bindings.SetProfessionWeightHandler(systems.ProfessionAssignments.SetWeight),
            logging.Log,
            dependencies.Recipes,
            dependencies.Constructions,
            dependencies.Geology,
            dependencies.NavigationTuning,
            dependencies.StockpilePresets);
    }

}
