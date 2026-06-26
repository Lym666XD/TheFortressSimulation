using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Definitions;

/// <summary>
/// Aggregated immutable catalog load result for data/core content used by a fortress session.
/// </summary>
internal sealed class CoreContentCatalogLoadResult
{
    internal CoreContentCatalogLoadResult(
        ItemDefinitionCatalogLoadResult items,
        CreatureDefinitionCatalogLoadResult creatures,
        CoreDataLoadResult coreData)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
        Creatures = creatures ?? throw new ArgumentNullException(nameof(creatures));
        CoreData = coreData ?? throw new ArgumentNullException(nameof(coreData));
    }

    internal ItemDefinitionCatalogLoadResult Items { get; }
    internal CreatureDefinitionCatalogLoadResult Creatures { get; }
    internal CoreDataLoadResult CoreData { get; }
    internal ConstructionContentLoadResult Constructions => CoreData.Constructions;
    internal RecipeContentLoadResult Recipes => CoreData.Recipes;

    internal bool HasErrors =>
        Items.ErrorCount > 0
        || Creatures.ErrorCount > 0
        || CoreData.HasErrors;
}

/// <summary>
/// Loads the data/core catalogs that used to be loaded through separate App/Core paths.
/// </summary>
internal static class CoreContentCatalogLoader
{
    internal static CoreContentCatalogLoadResult Load(string coreDataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coreDataPath);

        return new CoreContentCatalogLoadResult(
            ItemDefinitionCatalogLoader.Load(coreDataPath),
            CreatureDefinitionCatalogLoader.Load(coreDataPath),
            CoreDataRegistryLoader.Load(coreDataPath));
    }
}
