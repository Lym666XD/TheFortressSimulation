using HumanFortress.Content.Definitions;
using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Jobs;
using HumanFortress.Jobs.Craft;
using HumanFortress.Navigation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

public sealed class FortressRuntimeDependencies
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
    public string? MiningTuningJson => Tunings.MiningJson;
    public SchedulerTunings SchedulerTunings => Tunings.Scheduler;
    public WorkshopTunings WorkshopTunings => Tunings.Workshops;
    public ProfessionAssignments ProfessionAssignments => Workforce.ProfessionAssignments;

    public static FortressRuntimeDependencies Load(
        World world,
        string baseDir,
        FortressRuntimeContentSnapshot? content = null,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        content ??= FortressRuntimeContentSnapshotLoader.CaptureLoaded();

        return new FortressRuntimeDependencies(
            FortressRuntimeCatalogs.FromContent(content),
            FortressRuntimeTunings.FromContent(content, log),
            FortressRuntimeWorkforce.Load(world, baseDir, log));
    }
}

public sealed class FortressRuntimeCatalogs
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

public sealed class FortressRuntimeTunings
{
    private FortressRuntimeTunings(
        ConstructionTuning construction,
        string? miningJson,
        NavigationTuning navigation,
        PlaceableTuning placeable,
        SchedulerTunings scheduler,
        WorkshopTunings workshops)
    {
        Construction = construction;
        MiningJson = miningJson;
        Navigation = navigation;
        Placeable = placeable;
        Scheduler = scheduler;
        Workshops = workshops;
    }

    public ConstructionTuning Construction { get; }
    public string? MiningJson { get; }
    public NavigationTuning Navigation { get; }
    public PlaceableTuning Placeable { get; }
    public SchedulerTunings Scheduler { get; }
    public WorkshopTunings Workshops { get; }

    public static FortressRuntimeTunings FromContent(
        FortressRuntimeContentSnapshot content,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new FortressRuntimeTunings(
            ConstructionTuning.LoadFromJson(content.ConstructionTuningJson),
            content.MiningTuningJson,
            NavigationTuning.LoadFromJson(content.NavigationTuningJson),
            PlaceableTuning.LoadFromJson(content.PlaceableTuningJson),
            SchedulerTunings.LoadFromJson(content.SchedulerTuningJson, "runtime content snapshot", log),
            WorkshopTunings.LoadFromJson(content.WorkshopTuningJson, "runtime content snapshot", log));
    }
}

public sealed class FortressRuntimeWorkforce
{
    private FortressRuntimeWorkforce(ProfessionAssignments professionAssignments)
    {
        ProfessionAssignments = professionAssignments;
    }

    public ProfessionAssignments ProfessionAssignments { get; }

    public static FortressRuntimeWorkforce Load(World world, string baseDir, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var professionRegistry = ProfessionRegistry.Load(baseDir, log);
        return new FortressRuntimeWorkforce(new ProfessionAssignments(professionRegistry, world.Creatures));
    }
}
