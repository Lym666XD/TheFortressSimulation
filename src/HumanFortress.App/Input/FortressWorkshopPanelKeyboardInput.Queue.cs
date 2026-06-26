using HumanFortress.App.Runtime;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressWorkshopPanelKeyboardInput
{
    private static bool HandleQueueNavigation(Keyboard keyboard, UiStore ui, int queueCount)
    {
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

        return false;
    }

    private static bool HandleQueueMutation(
        Keyboard keyboard,
        IFortressRuntimeWorkshopPanelAccess runtime,
        UiStore ui,
        Guid workshopGuid,
        WorkshopSummaryView state,
        int queueCount,
        ulong uiTick)
    {
        if (keyboard.IsKeyPressed(Keys.A))
            return QueueDefaultRecipe(runtime, ui, workshopGuid, state.DefinitionId, uiTick);

        if ((keyboard.IsKeyPressed(Keys.Delete) || keyboard.IsKeyPressed(Keys.Back)) && queueCount > 0)
        {
            var entry = GetSelectedEntry(ui, state, queueCount);
            runtime.QueueRemoveWorkshopQueueEntry(workshopGuid, entry.EntryId);
            ui.WorkshopQueueSelectedIndex = Math.Max(0, ui.WorkshopQueueSelectedIndex - 1);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.PageUp) && queueCount > 0)
            return MoveSelectedQueueEntry(runtime, ui, workshopGuid, state, queueCount, -1);

        if (keyboard.IsKeyPressed(Keys.PageDown) && queueCount > 0)
            return MoveSelectedQueueEntry(runtime, ui, workshopGuid, state, queueCount, 1);

        return false;
    }
}
