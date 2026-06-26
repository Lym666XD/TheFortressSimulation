using HumanFortress.App.Runtime;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressWorkshopPanelKeyboardInput
{
    private static bool HandleWorkerSlots(
        Keyboard keyboard,
        IFortressRuntimeWorkshopPanelAccess runtime,
        Guid workshopGuid,
        int allowedWorkers)
    {
        if (keyboard.IsKeyPressed(Keys.OemPlus) || keyboard.IsKeyPressed(Keys.Add))
        {
            runtime.QueueSetWorkshopWorkerSlots(workshopGuid, allowedWorkers + 1);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.OemMinus) || keyboard.IsKeyPressed(Keys.Subtract))
        {
            runtime.QueueSetWorkshopWorkerSlots(workshopGuid, Math.Max(1, allowedWorkers - 1));
            return true;
        }

        return false;
    }

    private static bool HandleAutomationToggles(
        Keyboard keyboard,
        IFortressRuntimeWorkshopPanelAccess runtime,
        Guid workshopGuid)
    {
        if (keyboard.IsKeyPressed(Keys.S))
        {
            runtime.QueueToggleWorkshopAutoSupply(workshopGuid);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.O))
        {
            runtime.QueueToggleWorkshopAutoStockpile(workshopGuid);
            return true;
        }

        return false;
    }
}
