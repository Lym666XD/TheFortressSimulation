using HumanFortress.Content.Loading;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

public static class FortressRuntimeHostFactory
{
    public static SimulationRuntimeHost<SimulationRuntimeSystems> Create(
        World world,
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IEventBus eventBus,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
        string baseDir,
        FortressRuntimeContentSnapshot? content = null,
        FortressRuntimeLogging? logging = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tickScheduler);
        ArgumentNullException.ThrowIfNull(commandQueue);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(diffLog);
        ArgumentNullException.ThrowIfNull(itemsDiffLog);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        logging ??= FortressRuntimeLogging.None;

        var dependencies = FortressRuntimeDependencies.Load(world, baseDir, content, logging.Log);

        return new SimulationRuntimeHost<SimulationRuntimeSystems>(
            world,
            tickScheduler,
            commandQueue,
            eventBus,
            diffLog,
            itemsDiffLog,
            navigation,
            () => FortressRuntimeSystemsFactory.Create(
                world,
                diffLog,
                itemsDiffLog,
                navigation,
                dependencies,
                logging),
            (context, systems) => context.SetProfessionWeightHandler(systems.ProfessionAssignments.SetWeight),
            logging.Log,
            dependencies.Recipes,
            dependencies.Constructions,
            dependencies.Geology,
            dependencies.NavigationTuning);
    }
}
