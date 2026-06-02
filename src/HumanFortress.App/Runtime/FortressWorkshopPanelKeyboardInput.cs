using HumanFortress.App.Commands;
using HumanFortress.App.UI;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.Placeables;
using SadConsole.Input;

namespace HumanFortress.App.Runtime;

internal static class FortressWorkshopPanelKeyboardInput
{
    public static bool Handle(
        Keyboard keyboard,
        FortressRuntimeAccess runtime,
        UiStore ui,
        ulong uiTick,
        Func<Guid, FortressWorkshopPanelContext?> findWorkshop)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(findWorkshop);

        if (!ui.WorkshopPanelOpen || ui.OpenWorkshopGuid == null)
            return false;

        var workshopGuid = ui.OpenWorkshopGuid.Value;
        var context = findWorkshop(workshopGuid);
        if (context == null)
            return false;

        var state = context.Workshop;
        int queueCount = state.Queue.Count;

        if (keyboard.IsKeyPressed(Keys.Up))
        {
            ui.WorkshopQueueSelectedIndex = Math.Max(0, ui.WorkshopQueueSelectedIndex - 1);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.Down))
        {
            ui.WorkshopQueueSelectedIndex = Math.Min(Math.Max(0, queueCount - 1), ui.WorkshopQueueSelectedIndex + 1);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.A))
        {
            var recipeId = GetDefaultRecipeForWorkshop(context.WorkshopDefinitionId);
            if (recipeId == null)
                return false;

            runtime.EnqueueCurrentTickCommand(tick =>
                new UpdateWorkshopQueueCommand(tick, workshopGuid, WorkshopQueueOperation.AddRecipe, recipeId));
            ui.AddToast("Recipe queued", uiTick + 100);
            return true;
        }

        if ((keyboard.IsKeyPressed(Keys.Delete) || keyboard.IsKeyPressed(Keys.Back)) && queueCount > 0)
        {
            var entry = state.Queue[Math.Clamp(ui.WorkshopQueueSelectedIndex, 0, queueCount - 1)];
            runtime.EnqueueCurrentTickCommand(tick =>
                new UpdateWorkshopQueueCommand(tick, workshopGuid, WorkshopQueueOperation.RemoveEntry, entryId: entry.EntryId));
            ui.WorkshopQueueSelectedIndex = Math.Max(0, ui.WorkshopQueueSelectedIndex - 1);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.PageUp) && queueCount > 0)
        {
            var entry = state.Queue[Math.Clamp(ui.WorkshopQueueSelectedIndex, 0, queueCount - 1)];
            runtime.EnqueueCurrentTickCommand(tick =>
                new UpdateWorkshopQueueCommand(tick, workshopGuid, WorkshopQueueOperation.MoveEntry, entryId: entry.EntryId, moveOffset: -1));
            ui.WorkshopQueueSelectedIndex = Math.Max(0, ui.WorkshopQueueSelectedIndex - 1);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.PageDown) && queueCount > 0)
        {
            var entry = state.Queue[Math.Clamp(ui.WorkshopQueueSelectedIndex, 0, queueCount - 1)];
            runtime.EnqueueCurrentTickCommand(tick =>
                new UpdateWorkshopQueueCommand(tick, workshopGuid, WorkshopQueueOperation.MoveEntry, entryId: entry.EntryId, moveOffset: 1));
            ui.WorkshopQueueSelectedIndex = Math.Min(queueCount - 1, ui.WorkshopQueueSelectedIndex + 1);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.OemPlus) || keyboard.IsKeyPressed(Keys.Add))
        {
            runtime.EnqueueCurrentTickCommand(tick =>
                new UpdateWorkshopQueueCommand(tick, workshopGuid, WorkshopQueueOperation.SetWorkerSlots, intValue: state.AllowedWorkers + 1));
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.OemMinus) || keyboard.IsKeyPressed(Keys.Subtract))
        {
            runtime.EnqueueCurrentTickCommand(tick =>
                new UpdateWorkshopQueueCommand(tick, workshopGuid, WorkshopQueueOperation.SetWorkerSlots, intValue: Math.Max(1, state.AllowedWorkers - 1)));
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.S))
        {
            runtime.EnqueueCurrentTickCommand(tick =>
                new UpdateWorkshopQueueCommand(tick, workshopGuid, WorkshopQueueOperation.ToggleAutoSupply));
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.O))
        {
            runtime.EnqueueCurrentTickCommand(tick =>
                new UpdateWorkshopQueueCommand(tick, workshopGuid, WorkshopQueueOperation.ToggleAutoStockpile));
            return true;
        }

        return false;
    }

    private static string? GetDefaultRecipeForWorkshop(string? workshopId)
    {
        if (string.IsNullOrWhiteSpace(workshopId))
            return null;

        var recipes = RecipeRegistry.Instance.GetRecipesForWorkshop(workshopId);
        if (recipes.Count == 0)
            return null;

        return recipes[0].Id;
    }
}

internal sealed record FortressWorkshopPanelContext(WorkshopState Workshop, string? WorkshopDefinitionId);
