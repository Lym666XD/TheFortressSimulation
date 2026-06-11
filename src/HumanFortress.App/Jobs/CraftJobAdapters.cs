using HumanFortress.Core.Content.Registry;
using HumanFortress.Jobs.Craft;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Jobs;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.App.Jobs;

internal sealed class CraftRecipeCatalogAdapter : ICraftRecipeCatalog
{
    public RecipeDefinition? GetRecipe(string recipeId)
    {
        return ContentRegistry.Instance.Recipes.GetRecipe(recipeId);
    }
}

internal sealed class CraftProfessionCandidateSource : ICraftWorkerCandidateSource
{
    private readonly ProfessionAssignments _professions;
    private readonly WorkerSelectionStrategy _workerStrategy;

    public CraftProfessionCandidateSource(
        ProfessionAssignments professions,
        WorkerSelectionStrategy workerStrategy)
    {
        _professions = professions;
        _workerStrategy = workerStrategy;
    }

    public IEnumerable<CreatureInstance> SelectCandidates(
        WorldModel world,
        string jobTag,
        HashSet<Guid> busy,
        ReservationManager reservations,
        ulong currentTick,
        Point3 referencePoint)
    {
        return _professions.SelectCandidates(
            world,
            jobTag,
            _workerStrategy,
            busy,
            reservations,
            currentTick,
            referencePoint);
    }
}
