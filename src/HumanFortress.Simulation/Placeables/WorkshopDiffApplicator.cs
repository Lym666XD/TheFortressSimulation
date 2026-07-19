using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Diagnostics;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Placeables;

internal static class WorkshopDiffApplicator
{
    internal static void ApplyAll(
        SimulationWorld world,
        IReadOnlyList<WorkshopDiff> diffs,
        IConstructionCatalog constructions)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffs);
        ArgumentNullException.ThrowIfNull(constructions);

        if (diffs.Count == 0)
            return;

        foreach (var diff in diffs.OrderBy(static d => d.GetSortKey()))
        {
            try
            {
                Apply(world, constructions, diff);
            }
            catch (Exception ex)
            {
                SimulationDiagnostics.Error(
                    world.Diagnostics,
                    "Simulation.WorkshopDiff",
                    $"[WorkshopDiffApplicator] Failed to apply diff {diff.Op}: {ex.Message}",
                    ex);
                throw;
            }
        }
    }

    private static void Apply(SimulationWorld world, IConstructionCatalog constructions, WorkshopDiff diff)
    {
        if (!TryGetWorkshopState(world, constructions, diff.WorkshopGuid, out var state))
        {
            throw new InvalidOperationException(
                $"Workshop {diff.WorkshopGuid} does not resolve to one owned placeable.");
        }

        switch (diff.Op)
        {
            case WorkshopDiffOp.AddRecipe:
                if (!string.IsNullOrWhiteSpace(diff.RecipeId))
                    state.AddEntry(diff.RecipeId, diff.WorkshopGuid, diff.CurrentTick);
                break;

            case WorkshopDiffOp.RemoveEntry:
                if (diff.EntryId.HasValue)
                    state.RemoveEntry(diff.EntryId.Value);
                break;

            case WorkshopDiffOp.MoveEntry:
                if (diff.EntryId.HasValue)
                    state.MoveEntry(diff.EntryId.Value, diff.MoveOffset);
                break;

            case WorkshopDiffOp.ClearQueue:
                state.ClearQueue();
                break;

            case WorkshopDiffOp.SetWorkerSlots:
                state.SetAllowedWorkers(diff.IntValue);
                break;

            case WorkshopDiffOp.SetAutoStockpile:
                state.AutoStockpileOutputs = diff.BoolValue ?? !state.AutoStockpileOutputs;
                break;

            case WorkshopDiffOp.SetAutoSupply:
                state.AutoRequestMaterials = diff.BoolValue ?? !state.AutoRequestMaterials;
                break;
        }
    }

    private static bool TryGetWorkshopState(
        SimulationWorld world,
        IConstructionCatalog constructions,
        Guid workshopGuid,
        out WorkshopState state)
    {
        foreach (var chunk in world.GetAllChunks())
        {
            var placeableData = chunk.GetPlaceableData();
            if (placeableData == null)
                continue;

            foreach (var placeable in placeableData.GetAllOwnedPlaceables())
            {
                if (placeable.Guid != workshopGuid)
                    continue;

                placeable.Workshop ??= new WorkshopState();
                var definition = constructions.GetConstruction(placeable.DefinitionId);
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
