using HumanFortress.App.UI;
using HumanFortress.App.Runtime;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressWorkshopPanelKeyboardInput
{
    public static bool Handle(
        Keyboard keyboard,
        IFortressRuntimeWorkshopPanelAccess runtime,
        UiStore ui,
        ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(ui);

        if (!ui.WorkshopPanelOpen || ui.OpenWorkshopGuid == null)
            return false;

        var workshopGuid = ui.OpenWorkshopGuid.Value;
        var workshop = runtime.GetWorkshopPanelData(workshopGuid);
        if (!workshop.HasValue)
            return false;

        var state = workshop.Value;
        int queueCount = state.Queue.Count;

        return HandleQueueNavigation(keyboard, ui, queueCount)
            || HandleQueueMutation(keyboard, runtime, ui, workshopGuid, state, queueCount, uiTick)
            || HandleWorkerSlots(keyboard, runtime, workshopGuid, state.AllowedWorkers)
            || HandleAutomationToggles(keyboard, runtime, workshopGuid);
    }
}
