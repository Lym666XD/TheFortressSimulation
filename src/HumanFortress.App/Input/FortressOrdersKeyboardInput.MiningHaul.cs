using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressOrdersKeyboardInput
{
    private static bool HandleMining(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick)
    {
        if (keyboard.IsKeyPressed(Keys.Z))
            return StartMining(ui, currentZ, uiTick, MiningAction.Dig, "Mining: Dig - select first corner");
        if (keyboard.IsKeyPressed(Keys.X))
            return StartMining(ui, currentZ, uiTick, MiningAction.DigStairwell, "Mining: Stairwell - select first corner");
        if (keyboard.IsKeyPressed(Keys.C))
            return StartMining(ui, currentZ, uiTick, MiningAction.DigRamp, "Mining: Ramp - select first corner");
        if (keyboard.IsKeyPressed(Keys.V))
            return StartMining(ui, currentZ, uiTick, MiningAction.DigChannel, "Mining: Channel - select first corner");
        if (keyboard.IsKeyPressed(Keys.F))
            return StartMining(ui, currentZ, uiTick, MiningAction.RemoveDigging, "Mining: Remove designations - select first corner");
        if (keyboard.IsKeyPressed(Keys.OemComma)) { ui.CancelPlacement(); return true; }
        return false;
    }

    private static bool StartMining(UiStore ui, int currentZ, ulong uiTick, MiningAction action, string toast)
    {
        ui.SelectedMiningAction = action;
        ui.StartPlacement(PlacementMode.MiningFirstCorner, currentZ);
        ui.AddToast(toast, uiTick + 120);
        return true;
    }

    private static bool HandleHaul(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick)
    {
        if (keyboard.IsKeyPressed(Keys.Z))
        {
            ui.StartPlacement(PlacementMode.HaulFirstCorner, currentZ);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.X))
        {
            ui.AddToast("Emergency haul: WIP", uiTick + 120);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.OemComma))
        {
            ui.CancelPlacement();
            return true;
        }

        return false;
    }
}
