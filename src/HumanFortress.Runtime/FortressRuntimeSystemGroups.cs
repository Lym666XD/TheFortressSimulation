using HumanFortress.Jobs;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Craft;
using HumanFortress.Navigation;
using HumanFortress.Runtime.Jobs;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

public sealed class FortressRuntimePlanningSystems
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

    public MiningSystem Mining { get; }
    public ITransportRequestQueue TransportQueue { get; }
    public HaulingSystem Hauling { get; }
    public ConstructionMaterialsPlanner ConstructionMaterials { get; }
    public ConstructionSystem Construction { get; }
    public BuildableConstructionSystem Buildable { get; }
    public CraftPlanner Craft { get; }

    public static FortressRuntimePlanningSystems Create(
        World world,
        FortressRuntimeDependencies dependencies,
        FortressRuntimeLogging? logging = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(dependencies);

        logging ??= FortressRuntimeLogging.None;

        var mining = new MiningSystem(world, world.Orders);
        var transportQueue = new TransportRequestQueue();
        var hauling = new HaulingSystem(world, world.Orders, transportIntake: transportQueue);
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

public sealed class FortressRuntimeJobSystems
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

    public MiningJobSystem Mining { get; }
    public TransportJobSystem Transport { get; }
    public ConstructionJobSystem Construction { get; }
    public CraftJobSystem Craft { get; }

    public static FortressRuntimeJobSystems Create(
        World world,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
        FortressRuntimeDependencies dependencies,
        FortressRuntimePlanningSystems planners,
        FortressRuntimeLogging? logging = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffLog);
        ArgumentNullException.ThrowIfNull(itemsDiffLog);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(planners);

        logging ??= FortressRuntimeLogging.None;

        var schedulerTunings = dependencies.SchedulerTunings;
        var professions = dependencies.ProfessionAssignments;
        var log = logging.Log;

        var mining = new MiningJobSystem(
            world,
            planners.Mining,
            diffLog,
            itemsDiffLog,
            navigation,
            intakeBudget: schedulerTunings.Mining.PlanPerTick,
            carryoverMaxTicks: schedulerTunings.BackpressureMaxCarryoverTicks,
            professions: professions,
            workerStrategy: schedulerTunings.WorkerSelection,
            navigationTuning: dependencies.NavigationTuning,
            miningTuningJson: dependencies.MiningTuningJson,
            geology: dependencies.Geology,
            log: log);

        var transport = new TransportJobSystem(
            world,
            planners.TransportQueue,
            diffLog,
            navigation,
            itemsDiffLog: itemsDiffLog,
            intakeBudget: schedulerTunings.Hauling.PlanPerTick,
            carryoverMaxTicks: schedulerTunings.BackpressureMaxCarryoverTicks,
            maxActiveJobs: schedulerTunings.HaulingLimits.MaxActive,
            professions: professions,
            workerStrategy: schedulerTunings.WorkerSelection,
            navigationTuning: dependencies.NavigationTuning,
            log: log);

        var construction = new ConstructionJobSystem(
            world,
            planners.Construction,
            diffLog,
            itemsDiffLog,
            dependencies.Constructions,
            dependencies.ConstructionTuning,
            dependencies.PlaceableTuning,
            maxPerTick: schedulerTunings.Construction.PlanPerTick,
            log: log);

        var craft = new CraftJobSystem(
            world,
            planners.Craft,
            dependencies.CraftRecipes,
            dependencies.Constructions,
            itemsDiffLog,
            navigation,
            professions,
            schedulerTunings.WorkerSelection,
            dependencies.NavigationTuning);

        return new FortressRuntimeJobSystems(mining, transport, construction, craft);
    }
}
