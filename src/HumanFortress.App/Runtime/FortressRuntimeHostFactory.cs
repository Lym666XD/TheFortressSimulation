using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Content.Loading;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

internal static class FortressRuntimeHostFactory
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
        FortressRuntimeContentSnapshot? content = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tickScheduler);
        ArgumentNullException.ThrowIfNull(commandQueue);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(diffLog);
        ArgumentNullException.ThrowIfNull(itemsDiffLog);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var dependencies = FortressRuntimeDependencies.Load(world, baseDir, content);

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
                dependencies),
            (context, systems) => context.SetProfessionWeightHandler(systems.ProfessionAssignments.SetWeight),
            Logger.Log,
            dependencies.Recipes,
            dependencies.Constructions,
            dependencies.Geology,
            dependencies.NavigationTuning);
    }
}
