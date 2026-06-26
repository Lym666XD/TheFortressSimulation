using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Jobs.Craft;

namespace HumanFortress.Runtime;

internal sealed class RuntimeCraftRecipeCatalogAdapter : ICraftRecipeCatalog
{
    private readonly IRecipeCatalog _recipes;

    internal RuntimeCraftRecipeCatalogAdapter(IRecipeCatalog recipes)
    {
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
    }

    RecipeDefinition? ICraftRecipeCatalog.GetRecipe(string recipeId)
    {
        return _recipes.GetRecipe(recipeId);
    }
}
