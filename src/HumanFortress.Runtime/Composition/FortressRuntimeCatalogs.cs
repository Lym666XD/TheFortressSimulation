using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Jobs.Craft;
using HumanFortress.Runtime.Content;

namespace HumanFortress.Runtime.Composition;

internal sealed class FortressRuntimeCatalogs
{
    private FortressRuntimeCatalogs(
        IConstructionCatalog constructions,
        IRecipeCatalog recipes,
        IRuntimeGeologyCatalog geology,
        ICraftRecipeCatalog craftRecipes,
        IReadOnlyDictionary<string, IReadOnlyList<string>> workshopCategoryTags)
    {
        Constructions = constructions;
        Recipes = recipes;
        Geology = geology;
        CraftRecipes = craftRecipes;
        WorkshopCategoryTags = workshopCategoryTags;
    }

    internal IConstructionCatalog Constructions { get; }
    internal IRecipeCatalog Recipes { get; }
    internal IRuntimeGeologyCatalog Geology { get; }
    internal ICraftRecipeCatalog CraftRecipes { get; }
    internal IReadOnlyDictionary<string, IReadOnlyList<string>> WorkshopCategoryTags { get; }

    internal static FortressRuntimeCatalogs FromContent(FortressRuntimeContentSnapshot content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new FortressRuntimeCatalogs(
            content.Constructions,
            content.Recipes,
            content.Geology,
            new RuntimeCraftRecipeCatalogAdapter(content.Recipes),
            content.WorkshopCategoryTags);
    }
}
