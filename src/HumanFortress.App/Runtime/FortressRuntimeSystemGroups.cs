using HumanFortress.App.Jobs;
using HumanFortress.Content.Loading;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Craft;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

internal sealed class FortressRuntimeDependencies
{
    private FortressRuntimeDependencies(
        FortressRuntimeCatalogs catalogs,
        FortressRuntimeTunings tunings,
        FortressRuntimeWorkforce workforce)
    {
        Catalogs = catalogs;
        Tunings = tunings;
        Workforce = workforce;
    }

    public FortressRuntimeCatalogs Catalogs { get; }
    public FortressRuntimeTunings Tunings { get; }
    public FortressRuntimeWorkforce Workforce { get; }

    public IConstructionCatalog Constructions => Catalogs.Constructions;
    public IRecipeCatalog Recipes => Catalogs.Recipes;
    public IRuntimeGeologyCatalog Geology => Catalogs.Geology;
    public ICraftRecipeCatalog CraftRecipes => Catalogs.CraftRecipes;
    public ConstructionTuning ConstructionTuning => Tunings.Construction;
    public NavigationTuning NavigationTuning => Tunings.Navigation;
    public PlaceableTuning PlaceableTuning => Tunings.Placeable;
    public SchedulerTunings SchedulerTunings => Tunings.Scheduler;
    public WorkshopTunings WorkshopTunings => Tunings.Workshops;
    public ProfessionAssignments ProfessionAssignments => Workforce.ProfessionAssignments;

    public static FortressRuntimeDependencies Load(World world, string baseDir)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var content = FortressRuntimeContentSnapshotLoader.CaptureLoaded();

        return new FortressRuntimeDependencies(
            FortressRuntimeCatalogs.FromContent(content),
            FortressRuntimeTunings.FromContent(content),
            FortressRuntimeWorkforce.Load(world, baseDir));
    }
}

internal sealed class FortressRuntimeCatalogs
{
    private FortressRuntimeCatalogs(
        IConstructionCatalog constructions,
        IRecipeCatalog recipes,
        IRuntimeGeologyCatalog geology,
        ICraftRecipeCatalog craftRecipes)
    {
        Constructions = constructions;
        Recipes = recipes;
        Geology = geology;
        CraftRecipes = craftRecipes;
    }

    public IConstructionCatalog Constructions { get; }
    public IRecipeCatalog Recipes { get; }
    public IRuntimeGeologyCatalog Geology { get; }
    public ICraftRecipeCatalog CraftRecipes { get; }

    public static FortressRuntimeCatalogs FromContent(FortressRuntimeContentSnapshot content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new FortressRuntimeCatalogs(
            content.Constructions,
            content.Recipes,
            content.Geology,
            new CraftRecipeCatalogAdapter(content.Recipes));
    }
}

internal sealed class FortressRuntimeTunings
{
    private FortressRuntimeTunings(
        ConstructionTuning construction,
        NavigationTuning navigation,
        PlaceableTuning placeable,
        SchedulerTunings scheduler,
        WorkshopTunings workshops)
    {
        Construction = construction;
        Navigation = navigation;
        Placeable = placeable;
        Scheduler = scheduler;
        Workshops = workshops;
    }

    public ConstructionTuning Construction { get; }
    public NavigationTuning Navigation { get; }
    public PlaceableTuning Placeable { get; }
    public SchedulerTunings Scheduler { get; }
    public WorkshopTunings Workshops { get; }

    public static FortressRuntimeTunings FromContent(FortressRuntimeContentSnapshot content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new FortressRuntimeTunings(
            ConstructionTuning.LoadFromJson(content.ConstructionTuningJson),
            NavigationTuning.LoadFromJson(content.NavigationTuningJson),
            PlaceableTuning.LoadFromJson(content.PlaceableTuningJson),
            SchedulerTunings.LoadFromJson(content.SchedulerTuningJson, "runtime content snapshot"),
            WorkshopTunings.LoadFromJson(content.WorkshopTuningJson, "runtime content snapshot"));
    }
}

internal sealed class FortressRuntimeWorkforce
{
    private FortressRuntimeWorkforce(ProfessionAssignments professionAssignments)
    {
        ProfessionAssignments = professionAssignments;
    }

    public ProfessionAssignments ProfessionAssignments { get; }

    public static FortressRuntimeWorkforce Load(World world, string baseDir)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var professionRegistry = ProfessionRegistry.Load(baseDir);
        return new FortressRuntimeWorkforce(new ProfessionAssignments(professionRegistry, world.Creatures));
    }
}

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

    public MiningSystem Mining { get; }
    public ITransportRequestQueue TransportQueue { get; }
    public HaulingSystem Hauling { get; }
    public ConstructionMaterialsPlanner ConstructionMaterials { get; }
    public ConstructionSystem Construction { get; }
    public BuildableConstructionSystem Buildable { get; }
    public CraftPlanner Craft { get; }

    public static FortressRuntimePlanningSystems Create(World world, FortressRuntimeDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(dependencies);

        var mining = new MiningSystem(world, world.Orders);
        var transportQueue = new TransportRequestQueue();
        var hauling = new HaulingSystem(world, world.Orders, transportIntake: transportQueue);
        var constructionMaterials = new ConstructionMaterialsPlanner(world, transportQueue, world.Items);
        ConstructionMaterialsPlanner.LogCallback = Logger.CreateCallback("Jobs.ConstructionMaterials");
        var construction = new ConstructionSystem(
            world,
            world.Orders,
            new ConstructionTerrainMaterialResolver(),
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

internal sealed class FortressRuntimeJobSystems
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
        FortressRuntimePlanningSystems planners)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffLog);
        ArgumentNullException.ThrowIfNull(itemsDiffLog);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(planners);

        var schedulerTunings = dependencies.SchedulerTunings;
        var professions = dependencies.ProfessionAssignments;

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
            navigationTuning: dependencies.NavigationTuning);

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
            navigationTuning: dependencies.NavigationTuning);

        var construction = new ConstructionJobSystem(
            world,
            planners.Construction,
            diffLog,
            itemsDiffLog,
            dependencies.Constructions,
            dependencies.ConstructionTuning,
            dependencies.PlaceableTuning,
            maxPerTick: schedulerTunings.Construction.PlanPerTick);

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
