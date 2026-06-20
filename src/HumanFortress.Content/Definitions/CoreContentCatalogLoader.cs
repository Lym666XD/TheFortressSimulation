using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Definitions;

/// <summary>
/// Aggregated immutable catalog load result for data/core content used by a fortress session.
/// </summary>
public sealed class CoreContentCatalogLoadResult
{
    public CoreContentCatalogLoadResult(
        ItemDefinitionCatalogLoadResult items,
        CreatureDefinitionCatalogLoadResult creatures,
        CoreDataLoadResult coreData)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
        Creatures = creatures ?? throw new ArgumentNullException(nameof(creatures));
        CoreData = coreData ?? throw new ArgumentNullException(nameof(coreData));
    }

    public ItemDefinitionCatalogLoadResult Items { get; }
    public CreatureDefinitionCatalogLoadResult Creatures { get; }
    public CoreDataLoadResult CoreData { get; }
    public ConstructionContentLoadResult Constructions => CoreData.Constructions;
    public RecipeContentLoadResult Recipes => CoreData.Recipes;

    public bool HasErrors =>
        Items.ErrorCount > 0
        || Creatures.ErrorCount > 0
        || CoreData.HasErrors;
}

/// <summary>
/// Loads the data/core catalogs that used to be loaded through separate App/Core paths.
/// </summary>
public static class CoreContentCatalogLoader
{
    public static CoreContentCatalogLoadResult Load(string coreDataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coreDataPath);

        return new CoreContentCatalogLoadResult(
            ItemDefinitionCatalogLoader.Load(coreDataPath),
            CreatureDefinitionCatalogLoader.Load(coreDataPath),
            CoreDataRegistryLoader.Load(coreDataPath));
    }
}
