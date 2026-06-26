using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static class FortressEscapeKeyboardInput
{
    public static bool Handle(
        Keyboard keyboard,
        UiStore ui,
        bool tilePanelOpen,
        StockpileUI? stockpileUI,
        Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        if (!keyboard.IsKeyPressed(Keys.Escape))
            return false;

        if (tilePanelOpen)
        {
            hideTilePanel();
            return true;
        }

        if (stockpileUI != null)
        {
            stockpileUI.CloseEditPopup();

            if (ui.Context == UiContext.PlacingTool)
                ui.CancelPlacement();
            else
                ui.Back();

            return true;
        }

        if (ui.OpenDrawer == DrawerId.None && ui.QuickMenu == QuickMenuKind.None && !ui.HelpOpen && !ui.DebugOpen)
        {
            ui.TogglePause();
            Logger.Log("[UI] ESC -> Toggle Pause overlay");
            return true;
        }

        ui.Back();
        Logger.Log($"[UI] ESC -> Back; drawer={ui.OpenDrawer} qmenu={ui.QuickMenu} help={ui.HelpOpen} debug={ui.DebugOpen}");
        return true;
    }
}
