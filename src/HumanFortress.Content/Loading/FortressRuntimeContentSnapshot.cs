using HumanFortress.Core.Content.Registry;
using StructuredContentRegistry = HumanFortress.Core.Content.Registry.ContentRegistry;

namespace HumanFortress.Content.Loading;

/// <summary>
/// Content-owned snapshot of runtime catalogs and tuning JSON used by App composition.
/// </summary>
public sealed class FortressRuntimeContentSnapshot
{
    public FortressRuntimeContentSnapshot(
        IConstructionCatalog constructions,
        IRecipeCatalog recipes,
        IRuntimeGeologyCatalog geology,
        string? constructionTuningJson,
        string? navigationTuningJson,
        string? placeableTuningJson,
        string? schedulerTuningJson,
        string? workshopTuningJson)
    {
        Constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
        Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        Geology = geology ?? throw new ArgumentNullException(nameof(geology));
        ConstructionTuningJson = constructionTuningJson;
        NavigationTuningJson = navigationTuningJson;
        PlaceableTuningJson = placeableTuningJson;
        SchedulerTuningJson = schedulerTuningJson;
        WorkshopTuningJson = workshopTuningJson;
    }

    public IConstructionCatalog Constructions { get; }
    public IRecipeCatalog Recipes { get; }
    public IRuntimeGeologyCatalog Geology { get; }
    public string? ConstructionTuningJson { get; }
    public string? NavigationTuningJson { get; }
    public string? PlaceableTuningJson { get; }
    public string? SchedulerTuningJson { get; }
    public string? WorkshopTuningJson { get; }
}

public static class FortressRuntimeContentSnapshotLoader
{
    public static FortressRuntimeContentSnapshot CaptureLoaded()
    {
        var registry = StructuredContentRegistry.Instance;

        return new FortressRuntimeContentSnapshot(
            registry.Constructions,
            registry.Recipes,
            registry,
            registry.GetTuningJson("tuning.construction", "$"),
            registry.GetTuningJson("tuning.navigation", "$"),
            registry.GetTuningJson("tuning.placeable", "$"),
            registry.GetTuningJson("tuning.scheduler", "$"),
            registry.GetTuningJson("tuning.workshops", "$"));
    }
}
