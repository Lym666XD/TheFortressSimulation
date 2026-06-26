using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal sealed partial class WorkshopQueueCommandTarget : IWorkshopQueueCommandTarget
{
    private readonly World _world;
    private readonly IRecipeCatalog _recipes;
    private readonly IConstructionCatalog _constructions;

    internal WorkshopQueueCommandTarget(
        World world,
        IRecipeCatalog recipes,
        IConstructionCatalog constructions)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
    }

    bool IWorkshopQueueCommandTarget.AddWorkshopRecipe(Guid workshopGuid, string recipeId, ulong currentTick)
    {
        if (string.IsNullOrWhiteSpace(recipeId)) return false;
        if (!TryGetWorkshopState(workshopGuid, out var state)) return false;

        var recipe = _recipes.GetRecipe(recipeId);
        if (recipe == null) return false;

        state.AddEntry(recipe.Id, recipe.Name, workshopGuid, currentTick);
        return true;
    }

    bool IWorkshopQueueCommandTarget.RemoveWorkshopQueueEntry(Guid workshopGuid, Guid entryId)
    {
        return TryGetWorkshopState(workshopGuid, out var state)
            && state.RemoveEntry(entryId);
    }

    bool IWorkshopQueueCommandTarget.MoveWorkshopQueueEntry(Guid workshopGuid, Guid entryId, int moveOffset)
    {
        return TryGetWorkshopState(workshopGuid, out var state)
            && state.MoveEntry(entryId, moveOffset);
    }

    bool IWorkshopQueueCommandTarget.ClearWorkshopQueue(Guid workshopGuid)
    {
        if (!TryGetWorkshopState(workshopGuid, out var state)) return false;

        state.ClearQueue();
        return true;
    }

    bool IWorkshopQueueCommandTarget.SetWorkshopWorkerSlots(Guid workshopGuid, int workerSlots)
    {
        if (!TryGetWorkshopState(workshopGuid, out var state)) return false;

        state.SetAllowedWorkers(workerSlots);
        return true;
    }

    bool IWorkshopQueueCommandTarget.SetWorkshopAutoStockpile(Guid workshopGuid, bool? value)
    {
        if (!TryGetWorkshopState(workshopGuid, out var state)) return false;

        state.AutoStockpileOutputs = value ?? !state.AutoStockpileOutputs;
        return true;
    }

    bool IWorkshopQueueCommandTarget.SetWorkshopAutoSupply(Guid workshopGuid, bool? value)
    {
        if (!TryGetWorkshopState(workshopGuid, out var state)) return false;

        state.AutoRequestMaterials = value ?? !state.AutoRequestMaterials;
        return true;
    }
}
