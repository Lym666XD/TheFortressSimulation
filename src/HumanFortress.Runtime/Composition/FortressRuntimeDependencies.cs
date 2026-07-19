using HumanFortress.Content.Definitions;
using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Jobs.Configuration;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Profession;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Content;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Composition;

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

    internal FortressRuntimeCatalogs Catalogs { get; }
    internal FortressRuntimeTunings Tunings { get; }
    internal FortressRuntimeWorkforce Workforce { get; }

    internal IConstructionCatalog Constructions => Catalogs.Constructions;
    internal IRecipeCatalog Recipes => Catalogs.Recipes;
    internal IRuntimeGeologyCatalog Geology => Catalogs.Geology;
    internal ICraftRecipeCatalog CraftRecipes => Catalogs.CraftRecipes;
    internal IReadOnlyDictionary<string, IReadOnlyList<string>> WorkshopCategoryTags =>
        Catalogs.WorkshopCategoryTags;
    internal ConstructionTuning ConstructionTuning => Tunings.Construction;
    internal NavigationTuning NavigationTuning => Tunings.Navigation;
    internal PlaceableTuning PlaceableTuning => Tunings.Placeable;
    internal string? MiningTuningJson => Tunings.MiningJson;
    internal SchedulerTunings SchedulerTunings => Tunings.Scheduler;
    internal WorkshopTunings WorkshopTunings => Tunings.Workshops;
    internal FortressRuntimeStockpilePresetCatalog StockpilePresets => Tunings.StockpilePresets;
    internal ProfessionAssignments ProfessionAssignments => Workforce.ProfessionAssignments;

    internal static FortressRuntimeDependencies Load(
        World world,
        string baseDir,
        FortressRuntimeContentSnapshot? content = null,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        content ??= SimulationWorldContentLoader.LoadCoreContent(world, baseDir, log: log);

        return new FortressRuntimeDependencies(
            FortressRuntimeCatalogs.FromContent(content),
            FortressRuntimeTunings.FromContent(content, baseDir, log),
            FortressRuntimeWorkforce.FromContent(world, content.Professions, baseDir, log));
    }
}
