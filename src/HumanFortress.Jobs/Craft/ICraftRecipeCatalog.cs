using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Jobs.Craft;

internal interface ICraftRecipeCatalog
{
    RecipeDefinition? GetRecipe(string recipeId);
}
