using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static class FortressStockpileKeyboardInput
{
    public static bool Handle(Keyboard keyboard, UiStore ui, int currentZ, ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (ui.StockpileMenu == StockpileSubmenu.None)
        {
            if (keyboard.IsKeyPressed(Keys.Z)) { ui.OpenStockpileSubmenu(StockpileSubmenu.Stockpile); return true; }
            if (keyboard.IsKeyPressed(Keys.X)) { ui.AddToast("Garbage dump: WIP", uiTick + 120); return true; }
            if (keyboard.IsKeyPressed(Keys.OemComma)) { ui.AddToast("Remove zone: WIP", uiTick + 120); return true; }
            return false;
        }

        if (keyboard.IsKeyPressed(Keys.Z))
        {
            ui.StartPlacement(PlacementMode.StockpileFirstCorner, currentZ);
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.OemComma))
        {
            ui.AddToast("Remove stockpile: WIP", uiTick + 120);
            return true;
        }

        return false;
    }
}
