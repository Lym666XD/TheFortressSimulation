using HumanFortress.App.UI;
using SadConsole.Input;

namespace HumanFortress.App.Runtime;

internal static class FortressGlobalUiKeyboardInput
{
    private static readonly (Keys Key, DrawerId Drawer, string Label)[] DrawerShortcuts =
    {
        (Keys.F1, DrawerId.Creature, "F1"),
        (Keys.F2, DrawerId.Stock, "F2"),
        (Keys.F3, DrawerId.Work, "F3"),
        (Keys.F4, DrawerId.PlacementManagement, "F4"),
        (Keys.F5, DrawerId.Military, "F5"),
        (Keys.F6, DrawerId.Country, "F6"),
        (Keys.F7, DrawerId.World, "F7"),
        (Keys.F8, DrawerId.Log, "F8")
    };

    private static readonly (Keys Key, QuickMenuKind Menu, string Label)[] QuickMenuShortcuts =
    {
        (Keys.Z, QuickMenuKind.Orders, "Z"),
        (Keys.X, QuickMenuKind.Zones, "X"),
        (Keys.C, QuickMenuKind.Build, "C"),
        (Keys.V, QuickMenuKind.Stockpile, "V")
    };

    public static bool HandleHelpAndDebug(Keyboard keyboard, UiStore ui)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        var changed = false;

        if (keyboard.IsKeyPressed(Keys.H))
        {
            ui.ToggleHelp();
            changed = true;
        }

        if (IsDebugTogglePressed(keyboard))
        {
            ui.ToggleDebug();
            Logger.Log($"[DEBUG] Toggle debug menu -> {ui.DebugOpen}");
            changed = true;
        }

        return changed;
    }

    public static bool TryHandleDrawerShortcut(Keyboard keyboard, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        foreach (var shortcut in DrawerShortcuts)
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

        foreach (var shortcut in QuickMenuShortcuts)
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

    public static bool HandleDrawerTabs(Keyboard keyboard, UiStore ui)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(ui);

        if (ui.Context != UiContext.Drawer || !keyboard.IsKeyPressed(Keys.Tab))
            return false;

        if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
            ui.TabPrev();
        else
            ui.TabNext();

        return true;
    }

    private static bool IsDebugTogglePressed(Keyboard keyboard)
    {
        if (keyboard.IsKeyPressed(Keys.F12))
            return true;

        if (keyboard.KeysPressed.Count == 0)
            return false;

        foreach (var keyInfo in keyboard.KeysPressed)
        {
            var name = keyInfo.Key.ToString();
            if (name.Contains("OemTilde", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Oem3", StringComparison.OrdinalIgnoreCase)
                || name.Contains("OemGrave", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Oem8", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Oem7", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Backquote", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
