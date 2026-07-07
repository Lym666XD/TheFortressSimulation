using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressGlobalUiKeyboardInput
{
    public static bool TryHandleDrawerShortcut(Keyboard keyboard, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        foreach (var shortcut in UiChromeSlots.DockSlots)
        {
            if (!keyboard.IsKeyPressed(shortcut.Key))
                continue;

            hideTilePanel();
            ui.OpenPanel(shortcut.Drawer);
            Logger.Log($"[KEY] {shortcut.Label} -> Drawer={ui.OpenDrawer}");
            return true;
        }

        return false;
    }

    public static bool HandleGlobalQuickMenus(Keyboard keyboard, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        if (ui.Context != UiContext.Global)
            return false;

        foreach (var shortcut in UiChromeSlots.QuickSlots)
        {
            if (!keyboard.IsKeyPressed(shortcut.Key))
                continue;

            hideTilePanel();
            ui.OpenQuickMenu(shortcut.Menu);
            Logger.Log($"[KEY] {shortcut.Label} -> QMenu={ui.QuickMenu}");
            return true;
        }

        return false;
    }
}
