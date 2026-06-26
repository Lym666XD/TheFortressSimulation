using HumanFortress.Simulation.Placeables;
using SadRogue.Primitives;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

internal sealed class CraftMaterialConsumer
{
    private readonly WorldModel _world;
    private readonly CraftWorkshopLocator _workshops;
    private readonly ICraftRecipeCatalog _recipes;
    private readonly ICraftDiffEmitter _diffEmitter;

    internal CraftMaterialConsumer(
        WorldModel world,
        CraftWorkshopLocator workshops,
        ICraftRecipeCatalog recipes,
        ICraftDiffEmitter diffEmitter)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _workshops = workshops ?? throw new ArgumentNullException(nameof(workshops));
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
    }

    internal bool TryConsumeInputs(ActiveCraftJob job)
    {
        if (!_workshops.TryFind(job.WorkshopGuid, out var placeable, out var state) || placeable == null || state == null)
        {
            return false;
        }

        var entry = state.GetEntry(job.QueueEntryId);
        if (entry == null)
        {
            return false;
        }

        var recipe = _recipes.GetRecipe(job.RecipeId);
        if (recipe == null)
        {
            return false;
        }

        var planned = new List<PlannedItemConsumption>();
        var plannedByItem = new Dictionary<Guid, int>();

        foreach (var ingredient in recipe.Inputs)
        {
            int remaining = ingredient.Count;
            var inputCells = CraftWorkshopLocator.EnumerateFootprintAndRing(placeable).ToHashSet();
            var candidates = _world.Items.GetGroundInstances()
                .Where(item => item.DefinitionId == ingredient.DefId
                    && item.Z == placeable.Z
                    && inputCells.Contains((item.Position.X, item.Position.Y)))
                .OrderBy(item => item.Guid)
                .ToList();

            foreach (var item in candidates)
            {
                if (remaining <= 0)
                {
                    break;
                }

                plannedByItem.TryGetValue(item.Guid, out int alreadyPlanned);
                int available = Math.Max(0, item.StackCount - alreadyPlanned);
                int take = Math.Min(available, remaining);
                if (take <= 0)
                {
                    continue;
                }

                plannedByItem[item.Guid] = alreadyPlanned + take;
                planned.Add(new PlannedItemConsumption(item.Guid, item.Position, item.Z, take));
                remaining -= take;
            }

            if (remaining > 0)
            {
                entry.Status = CraftQueueStatus.AwaitingMaterials;
                entry.BlockingReason = $"Need {remaining}x {ingredient.DefId}";
                entry.ActiveWorkerId = null;
                return false;
            }
        }

        foreach (var consumption in planned)
        {
            _diffEmitter.RemoveItem(consumption.ItemGuid, consumption.Position, consumption.Z, consumption.Quantity);
        }

        entry.BlockingReason = null;
        return true;
    }

    private readonly record struct PlannedItemConsumption(Guid ItemGuid, Point Position, int Z, int Quantity);
}
