using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

public sealed class WorkshopQueueCommandTarget : IWorkshopQueueCommandTarget
{
    private readonly World _world;
    private readonly IRecipeCatalog _recipes;
    private readonly IConstructionCatalog _constructions;

    public WorkshopQueueCommandTarget(
        World world,
        IRecipeCatalog recipes,
        IConstructionCatalog constructions)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
    }

    public bool AddWorkshopRecipe(Guid workshopGuid, string recipeId, ulong currentTick)
    {
        if (string.IsNullOrWhiteSpace(recipeId)) return false;
        if (!TryGetWorkshopState(workshopGuid, out var state)) return false;

        var recipe = _recipes.GetRecipe(recipeId);
        if (recipe == null) return false;

        state.AddEntry(recipe.Id, recipe.Name, workshopGuid, currentTick);
        return true;
    }

    public bool RemoveWorkshopQueueEntry(Guid workshopGuid, Guid entryId)
    {
        return TryGetWorkshopState(workshopGuid, out var state)
            && state.RemoveEntry(entryId);
    }

    public bool MoveWorkshopQueueEntry(Guid workshopGuid, Guid entryId, int moveOffset)
    {
        return TryGetWorkshopState(workshopGuid, out var state)
            && state.MoveEntry(entryId, moveOffset);
    }

    public bool ClearWorkshopQueue(Guid workshopGuid)
    {
        if (!TryGetWorkshopState(workshopGuid, out var state)) return false;

        state.ClearQueue();
        return true;
    }

    public bool SetWorkshopWorkerSlots(Guid workshopGuid, int workerSlots)
    {
        if (!TryGetWorkshopState(workshopGuid, out var state)) return false;

        state.SetAllowedWorkers(workerSlots);
        return true;
    }

    public bool SetWorkshopAutoStockpile(Guid workshopGuid, bool? value)
    {
        if (!TryGetWorkshopState(workshopGuid, out var state)) return false;

        state.AutoStockpileOutputs = value ?? !state.AutoStockpileOutputs;
        return true;
    }

    public bool SetWorkshopAutoSupply(Guid workshopGuid, bool? value)
    {
        if (!TryGetWorkshopState(workshopGuid, out var state)) return false;

        state.AutoRequestMaterials = value ?? !state.AutoRequestMaterials;
        return true;
    }

    private bool TryGetWorkshopState(Guid workshopGuid, out WorkshopState state)
    {
        foreach (var chunk in _world.GetAllChunks())
        {
            var placeableData = chunk.GetPlaceableData();
            if (placeableData == null) continue;

            foreach (var placeable in placeableData.GetAllOwnedPlaceables())
            {
                if (placeable.Guid != workshopGuid) continue;

                placeable.Workshop ??= new WorkshopState();
                var definition = _constructions.GetConstruction(placeable.DefinitionId);
                if (definition != null && placeable.Workshop.MaxWorkers <= 1)
                {
                    int maxWorkers = Math.Max(1, definition.Io?.InputSlots ?? 1);
                    placeable.Workshop.ConfigureWorkers(placeable.Workshop.AllowedWorkers, maxWorkers);
                }

                state = placeable.Workshop;
                return true;
            }
        }

        state = null!;
        return false;
    }
}
