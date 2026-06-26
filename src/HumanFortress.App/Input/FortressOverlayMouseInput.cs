using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static class FortressOverlayMouseInput
{
    public static bool IsInsideDebugWindow(Point local, int surfaceWidth, int surfaceHeight)
    {
        var debugWindow = DebugLayoutCalculator.CalculateWindow(surfaceWidth, surfaceHeight);
        return debugWindow.Contains(local);
    }

    public static bool TryHandleDockClick(Point local, int surfaceWidth, int surfaceHeight, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        var slot = ButtonLayoutCalculator.HitTestDockButtons(local, surfaceWidth, surfaceHeight);
        if (!slot.HasValue || !UiChromeSlots.TryGetDockSlot(slot.Value, out var dockSlot))
            return false;

        hideTilePanel();
        ui.OpenPanel(dockSlot.Drawer);
        Logger.Log($"[CLICK-OVERLAY] Dock slot={slot.Value} -> drawer={ui.OpenDrawer}");
        return true;
    }

    public static bool TryHandleQuickClick(Point local, int surfaceWidth, int surfaceHeight, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        var slot = ButtonLayoutCalculator.HitTestQuickButtons(local, surfaceWidth, surfaceHeight);
        if (!slot.HasValue || !UiChromeSlots.TryGetQuickSlot(slot.Value, out var quickSlot))
            return false;

        hideTilePanel();
        ui.OpenQuickMenu(quickSlot.Menu);
        Logger.Log($"[CLICK-OVERLAY] Quick kind={quickSlot.Menu} -> qmenu={ui.QuickMenu}");
        return true;
    }

    public static bool TryHandleDebugSpawnClick(Point local, int surfaceWidth, int surfaceHeight, UiStore ui, Point cursorPos, int currentZ, ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(ui);

        if (!ui.DebugOpen)
            return false;

        int width = Math.Min((int)(surfaceWidth * 0.7), surfaceWidth - 4);
        int height = Math.Min((int)(surfaceHeight * 0.6), surfaceHeight - 4);
        int x0 = (surfaceWidth - width) / 2;
        int y0 = (surfaceHeight - height) / 2;
        int buttonX = x0 + 2;
        int buttonY = y0 + 2;
        const int buttonWidth = 22;

        if (local.Y != buttonY || local.X < buttonX || local.X >= buttonX + buttonWidth)
            return false;

        Logger.Log($"[DEBUG] Spawn Dwarf click at cursor=({cursorPos.X},{cursorPos.Y},{currentZ}) [overlay]");
        ui.AddDebugDwarf(cursorPos, currentZ);
        ui.AddToast("Spawned dwarf (debug marker)", uiTick + 100);
        return true;
    }
}
