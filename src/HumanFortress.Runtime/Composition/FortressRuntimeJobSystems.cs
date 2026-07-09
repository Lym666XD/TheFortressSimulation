using HumanFortress.Core.Simulation;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Navigation;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Composition;

internal sealed partial class FortressRuntimeJobSystems
{
    private FortressRuntimeJobSystems(
        MiningJobSystem mining,
        TransportJobSystem transport,
        ConstructionJobSystem construction,
        CraftJobSystem craft)
    {
        Mining = mining;
        Transport = transport;
        Construction = construction;
        Craft = craft;
    }

    internal MiningJobSystem Mining { get; }
    internal TransportJobSystem Transport { get; }
    internal ConstructionJobSystem Construction { get; }
    internal CraftJobSystem Craft { get; }

    internal static FortressRuntimeJobSystems Create(
        World world,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        StockpileDiffLog stockpileDiffLog,
        NavigationManager navigation,
        RuntimePathServiceRegistry pathServices,
        FortressRuntimeDependencies dependencies,
        FortressRuntimePlanningSystems planners,
        FortressRuntimeLogging? logging = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffLog);
        ArgumentNullException.ThrowIfNull(itemsDiffLog);
        ArgumentNullException.ThrowIfNull(stockpileDiffLog);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(pathServices);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(planners);

        logging ??= FortressRuntimeLogging.None;
        pathServices.Clear();

        var mining = CreateMining(world, diffLog, itemsDiffLog, navigation, dependencies, planners, logging);
        var transport = CreateTransport(world, diffLog, itemsDiffLog, stockpileDiffLog, navigation, dependencies, planners, logging);
        var construction = CreateConstruction(world, diffLog, itemsDiffLog, stockpileDiffLog, dependencies, planners, logging);
        var craft = CreateCraft(world, itemsDiffLog, stockpileDiffLog, navigation, dependencies, planners);

        pathServices.Register(mining.PathService);
        pathServices.Register(transport.PathService);
        pathServices.Register(craft.PathService);

        return new FortressRuntimeJobSystems(mining, transport, construction, craft);
    }
}
