using HumanFortress.Core.Content.Registry;

namespace HumanFortress.Jobs.Craft;

public interface ICraftRecipeCatalog
{
    RecipeDefinition? GetRecipe(string recipeId);
}
