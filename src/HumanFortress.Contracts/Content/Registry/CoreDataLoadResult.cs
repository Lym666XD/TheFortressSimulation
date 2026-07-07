using System;
using System.Collections.Generic;

namespace HumanFortress.Contracts.Content.Registry;

public sealed class CoreDataLoadResult
{
    public CoreDataLoadResult(
        ConstructionContentLoadResult constructions,
        RecipeContentLoadResult recipes)
    {
        Constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
        Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
    }

    public ConstructionContentLoadResult Constructions { get; }
    public RecipeContentLoadResult Recipes { get; }
    public bool HasErrors => Constructions.ErrorCount > 0 || Recipes.ErrorCount > 0;
}

public sealed class ConstructionContentLoadResult
{
    public ConstructionContentLoadResult(
        ConstructionCatalogStore catalog,
        int loadedCount,
        int errorCount,
        int duplicatesSkipped,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> messages)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        LoadedCount = loadedCount;
        ErrorCount = errorCount;
        DuplicatesSkipped = duplicatesSkipped;
        Categories = categories ?? throw new ArgumentNullException(nameof(categories));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    public ConstructionCatalogStore Catalog { get; }
    public int LoadedCount { get; }
    public int ErrorCount { get; }
    public int DuplicatesSkipped { get; }
    public IReadOnlyList<string> Categories { get; }
    public IReadOnlyList<string> Messages { get; }
}

public sealed class RecipeContentLoadResult
{
    public RecipeContentLoadResult(
        RecipeCatalogStore catalog,
        int loadedCount,
        int errorCount,
        IReadOnlyList<string> messages)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        LoadedCount = loadedCount;
        ErrorCount = errorCount;
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    public RecipeCatalogStore Catalog { get; }
    public int LoadedCount { get; }
    public int ErrorCount { get; }
    public IReadOnlyList<string> Messages { get; }
}
