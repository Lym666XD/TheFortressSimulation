using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Placeables;

namespace HumanFortress.Jobs.Craft;

internal sealed class CraftMaterialReadinessChecker
{
    private readonly CraftInputCounter _inputCounter;
    private readonly CraftTransportRequestEmitter _transportRequests;
    private readonly int _requestRetryTicks;

    internal CraftMaterialReadinessChecker(
        CraftInputCounter inputCounter,
        CraftTransportRequestEmitter transportRequests,
        int requestRetryTicks)
    {
        _inputCounter = inputCounter ?? throw new ArgumentNullException(nameof(inputCounter));
        _transportRequests = transportRequests ?? throw new ArgumentNullException(nameof(transportRequests));
        _requestRetryTicks = requestRetryTicks;
    }

    internal bool HasMaterials(
        PlaceableInstance placeable,
        WorkshopState state,
        CraftQueueEntry entry,
        RecipeDefinition recipe,
        ulong tick)
    {
        if (recipe.Inputs.Length == 0)
        {
            return true;
        }

        var delivered = _inputCounter.CountAvailableInputs(placeable, tick);
        foreach (var ingredient in recipe.Inputs)
        {
            delivered.TryGetValue(ingredient.DefId, out var have);
            if (have >= ingredient.Count)
            {
                continue;
            }

            entry.Status = CraftQueueStatus.AwaitingMaterials;
            entry.BlockingReason = $"Need {ingredient.Count}x {ingredient.DefId}";
            if (state.AutoRequestMaterials && (!entry.HasPendingRequests || tick - entry.LastRequestTick >= (ulong)_requestRetryTicks))
            {
                if (_transportRequests.RequestMaterials(placeable, ingredient.DefId, ingredient.Count - have, tick) > 0)
                {
                    entry.HasPendingRequests = true;
                    entry.LastRequestTick = tick;
                }
            }

            return false;
        }

        entry.HasPendingRequests = false;
        entry.Status = CraftQueueStatus.Pending;
        entry.BlockingReason = null;
        return true;
    }
}
