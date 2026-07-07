using HumanFortress.Jobs.Construction;
using HumanFortress.Jobs.Craft;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Composition;

internal sealed class FortressRuntimePlanningSystems
{
    private FortressRuntimePlanningSystems(
        MiningSystem mining,
        ITransportRequestQueue transportQueue,
        HaulingSystem hauling,
        ConstructionMaterialsPlanner constructionMaterials,
        ConstructionSystem construction,
        BuildableConstructionSystem buildable,
        CraftPlanner craft)
    {
        Mining = mining;
        TransportQueue = transportQueue;
        Hauling = hauling;
        ConstructionMaterials = constructionMaterials;
        Construction = construction;
        Buildable = buildable;
        Craft = craft;
    }

    internal MiningSystem Mining { get; }
    internal ITransportRequestQueue TransportQueue { get; }
    internal HaulingSystem Hauling { get; }
    internal ConstructionMaterialsPlanner ConstructionMaterials { get; }
    internal ConstructionSystem Construction { get; }
    internal BuildableConstructionSystem Buildable { get; }
    internal CraftPlanner Craft { get; }

    internal static FortressRuntimePlanningSystems Create(
        World world,
        StockpileDiffLog stockpileDiffLog,
        FortressRuntimeDependencies dependencies,
        FortressRuntimeLogging? logging = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(stockpileDiffLog);
        ArgumentNullException.ThrowIfNull(dependencies);

        logging ??= FortressRuntimeLogging.None;

        var mining = new MiningSystem(world, world.Orders);
        var transportQueue = new TransportRequestQueue();
        var hauling = new HaulingSystem(
            world,
            world.Orders,
            transportIntake: transportQueue,
            stockpileDiffLog: stockpileDiffLog);
        var constructionMaterials = new ConstructionMaterialsPlanner(world, transportQueue, world.Items);
        ConstructionMaterialsPlanner.LogCallback = logging.ConstructionMaterials;
        var construction = new ConstructionSystem(
            world,
            world.Orders,
            new ConstructionTerrainMaterialResolver(dependencies.Geology),
            dependencies.ConstructionTuning);
        var buildable = new BuildableConstructionSystem(world, world.Orders, dependencies.Constructions);
        var craft = new CraftPlanner(
            world,
            transportQueue,
            dependencies.CraftRecipes,
            dependencies.Constructions);

        return new FortressRuntimePlanningSystems(
            mining,
            transportQueue,
            hauling,
            constructionMaterials,
            construction,
            buildable,
            craft);
    }
}
