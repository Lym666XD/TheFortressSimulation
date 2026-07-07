using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressScreenMouseInput
{
    public static bool TryHandleDockClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        var slot = ButtonLayoutCalculator.HitTestDockButtons(screenCell, screenWidth, screenHeight);
        if (!slot.HasValue || !UiChromeSlots.TryGetDockSlot(slot.Value, out var dockSlot))
            return false;

        hideTilePanel();
        ui.OpenPanel(dockSlot.Drawer);
        Logger.Log($"[CLICK] DockScreen i={slot.Value} cell=({screenCell.X},{screenCell.Y}) -> drawer={ui.OpenDrawer}");
        return true;
    }

    public static bool TryHandleQuickIconClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        var slot = ButtonLayoutCalculator.HitTestQuickButtons(screenCell, screenWidth, screenHeight);
        if (!slot.HasValue || !UiChromeSlots.TryGetQuickSlot(slot.Value, out var quickSlot))
            return false;

        var kind = quickSlot.Menu;
        Logger.Log($"[CLICK] QuickIconsScreen HIT: kind={kind} cell=({screenCell.X},{screenCell.Y})");
        hideTilePanel();
        if (ui.QuickMenu == kind)
            ui.CancelPlacement();
        else
            ui.OpenQuickMenu(kind);

        Logger.Log($"[CLICK] QuickIconsScreen result: qmenu={ui.QuickMenu}");
        return true;
    }
}
