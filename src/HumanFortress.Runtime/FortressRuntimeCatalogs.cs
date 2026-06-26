using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Jobs.Craft;

namespace HumanFortress.Runtime;

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

    internal IConstructionCatalog Constructions { get; }
    internal IRecipeCatalog Recipes { get; }
    internal IRuntimeGeologyCatalog Geology { get; }
    internal ICraftRecipeCatalog CraftRecipes { get; }

    internal static FortressRuntimeCatalogs FromContent(FortressRuntimeContentSnapshot content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new FortressRuntimeCatalogs(
            content.Constructions,
            content.Recipes,
            content.Geology,
            new RuntimeCraftRecipeCatalogAdapter(content.Recipes));
    }
}
