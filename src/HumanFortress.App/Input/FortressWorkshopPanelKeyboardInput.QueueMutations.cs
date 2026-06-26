using HumanFortress.App.Runtime;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.Input;

internal static partial class FortressWorkshopPanelKeyboardInput
{
    private static bool QueueDefaultRecipe(
        IFortressRuntimeWorkshopPanelAccess runtime,
        UiStore ui,
        Guid workshopGuid,
        string definitionId,
        ulong uiTick)
    {
        var recipeId = runtime.GetDefaultRecipeForWorkshop(definitionId);
        if (recipeId == null)
            return false;

        runtime.QueueAddWorkshopRecipe(workshopGuid, recipeId);
        ui.AddToast("Recipe queued", uiTick + 100);
        return true;
    }

    private static bool MoveSelectedQueueEntry(
        IFortressRuntimeWorkshopPanelAccess runtime,
        UiStore ui,
        Guid workshopGuid,
        WorkshopSummaryView state,
        int queueCount,
        int moveOffset)
    {
        var entry = GetSelectedEntry(ui, state, queueCount);
        runtime.QueueMoveWorkshopQueueEntry(workshopGuid, entry.EntryId, moveOffset);
        ui.WorkshopQueueSelectedIndex = moveOffset < 0
            ? Math.Max(0, ui.WorkshopQueueSelectedIndex - 1)
            : Math.Min(queueCount - 1, ui.WorkshopQueueSelectedIndex + 1);
        return true;
    }

    private static WorkshopQueueEntryView GetSelectedEntry(UiStore ui, WorkshopSummaryView state, int queueCount)
    {
        return state.Queue[Math.Clamp(ui.WorkshopQueueSelectedIndex, 0, queueCount - 1)];
    }
}
