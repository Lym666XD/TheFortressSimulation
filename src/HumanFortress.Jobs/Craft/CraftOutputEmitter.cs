namespace HumanFortress.Jobs.Craft;

internal sealed class CraftOutputEmitter
{
    private readonly ICraftRecipeCatalog _recipes;
    private readonly ICraftDiffEmitter _diffEmitter;

    public CraftOutputEmitter(ICraftRecipeCatalog recipes, ICraftDiffEmitter diffEmitter)
    {
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
    }

    public void EmitOutputs(ActiveCraftJob job)
    {
        var recipe = _recipes.GetRecipe(job.RecipeId);
        if (recipe == null)
        {
            return;
        }

        foreach (var output in recipe.Outputs)
        {
            _diffEmitter.AddItem(job.Anchor, job.Z, output.DefId, output.Count);
        }
    }
}
