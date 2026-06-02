using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal static class FortressOverlayMouseInput
{
    private static readonly DrawerId[] DockSlots =
    {
        DrawerId.Creature,
        DrawerId.Stock,
        DrawerId.Work,
        DrawerId.PlacementManagement,
        DrawerId.Military,
        DrawerId.Country,
        DrawerId.World,
        DrawerId.Log
    };

    private static readonly QuickMenuKind[] QuickSlots =
    {
        QuickMenuKind.Orders,
        QuickMenuKind.Zones,
        QuickMenuKind.Build,
        QuickMenuKind.Stockpile
    };

    public static bool IsInsideDebugWindow(Point local, int surfaceWidth, int surfaceHeight)
    {
        var debugWindow = DebugLayoutCalculator.CalculateWindow(surfaceWidth, surfaceHeight);
        return debugWindow.Contains(local);
    }

    public static bool TryHandleDockClick(Point local, int surfaceHeight, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        const int dockXStart = 1;
        const int dockButtonWidth = 5;
        const int dockGap = 1;
        int dockY = surfaceHeight - 1;

        if (local.Y != dockY || local.X < dockXStart)
            return false;

        int slot = (local.X - dockXStart) / (dockButtonWidth + dockGap);
        if (slot < 0 || slot >= DockSlots.Length)
            return false;

        hideTilePanel();
        ui.OpenPanel(DockSlots[slot]);
        Logger.Log($"[CLICK-OVERLAY] Dock slot={slot} -> drawer={ui.OpenDrawer}");
        return true;
    }

    public static bool TryHandleQuickClick(Point local, int surfaceWidth, int surfaceHeight, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        int quickY = surfaceHeight - 1;
        if (local.Y != quickY)
            return false;

        const int buttonWidth = 5;
        const int gap = 2;
        int center = surfaceWidth / 2;
        int totalWidth = (buttonWidth * 4) + (gap * 3);
        int startX = center - totalWidth / 2;

        for (int slot = 0; slot < QuickSlots.Length; slot++)
        {
            int rangeStart = startX + (buttonWidth + gap) * slot;
            int rangeEnd = rangeStart + buttonWidth - 1;

            if (local.X < rangeStart || local.X > rangeEnd)
                continue;

            hideTilePanel();
            ui.OpenQuickMenu(QuickSlots[slot]);
            Logger.Log($"[CLICK-OVERLAY] Quick kind={QuickSlots[slot]} x=[{rangeStart},{rangeEnd}] -> qmenu={ui.QuickMenu}");
            return true;
        }

        return false;
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
