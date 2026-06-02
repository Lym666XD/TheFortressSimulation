using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Runtime;

internal static class FortressStockpilePresetKeyboardInput
{
    public static bool Handle(Keyboard keyboard, UiStore ui, StockpileUI? stockpileUI, Action<string> createStockpile)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(createStockpile);

        if (ui.Context != UiContext.PlacingTool || ui.PlaceMode != PlacementMode.StockpilePresetSelect)
            return false;

        for (int i = 1; i <= 9; i++)
        {
            if (!keyboard.IsKeyPressed((Keys)(Keys.D1 + i - 1)))
                continue;

            var presetId = stockpileUI?.HandlePresetSelection(i);
            if (presetId == null)
                return false;

            createStockpile(presetId);
            ui.CancelPlacement();
            return true;
        }

        if (!keyboard.IsKeyPressed(Keys.Enter))
            return false;

        createStockpile("all");
        ui.CancelPlacement();
        return true;
    }
}
