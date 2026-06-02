using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal static class FortressScreenMouseInput
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

    public static bool TryHandleDockClick(Point screenCell, int screenHeight, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        const int xStart = 1;
        const int buttonWidth = 5;
        const int gap = 1;
        int y = screenHeight - 1;

        if (screenCell.Y != y)
            return false;

        for (int slot = 0; slot < DockSlots.Length; slot++)
        {
            int start = xStart + slot * (buttonWidth + gap);
            int end = start + buttonWidth - 1;
            if (screenCell.X < start || screenCell.X > end)
                continue;

            hideTilePanel();
            ui.OpenPanel(DockSlots[slot]);
            Logger.Log($"[CLICK] DockScreen i={slot} cell=({screenCell.X},{screenCell.Y}) -> drawer={ui.OpenDrawer}");
            return true;
        }

        return false;
    }

    public static bool TryHandleQuickIconClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui, Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        int y = screenHeight - 1;
        if (screenCell.Y != y)
            return false;

        const int buttonWidth = 5;
        const int gap = 2;
        int center = screenWidth / 2;
        int totalWidth = (buttonWidth * 4) + (gap * 3);
        int startX = center - totalWidth / 2;

        for (int slot = 0; slot < QuickSlots.Length; slot++)
        {
            int start = startX + slot * (buttonWidth + gap);
            int end = start + buttonWidth - 1;
            if (screenCell.X < start || screenCell.X > end)
                continue;

            var kind = QuickSlots[slot];
            Logger.Log($"[CLICK] QuickIconsScreen HIT: kind={kind} cell=({screenCell.X},{screenCell.Y})");
            hideTilePanel();
            if (ui.QuickMenu == kind)
                ui.CancelPlacement();
            else
                ui.OpenQuickMenu(kind);

            Logger.Log($"[CLICK] QuickIconsScreen result: qmenu={ui.QuickMenu}");
            return true;
        }

        return false;
    }

    public static bool TryHandleQuickMenuClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui, int currentZ, ulong uiTick)
    {
        ArgumentNullException.ThrowIfNull(ui);

        if (ui.QuickMenu == QuickMenuKind.None)
            return false;

        int centerX = screenWidth / 2;

        if (ui.QuickMenu == QuickMenuKind.Orders && ui.OrdersMenu == OrdersSubmenu.None)
            return TryHandleOrdersRootClick(screenCell, screenWidth, screenHeight, ui);

        if (ui.QuickMenu == QuickMenuKind.Zones && ui.ZoneMenu == ZoneSubmenu.None)
            return TryHandleZonesRootClick(screenCell, screenWidth, screenHeight, ui);

        if (ui.QuickMenu == QuickMenuKind.Build && ui.BuildMenu == BuildSubmenu.None)
            return TryHandleBuildRootClick(screenCell, screenWidth, screenHeight, ui);

        if (ui.QuickMenu == QuickMenuKind.Orders && ui.OrdersMenu == OrdersSubmenu.Mining)
            return TryHandleMiningClick(screenCell, centerX, screenHeight, ui, currentZ, uiTick);

        if (ui.QuickMenu == QuickMenuKind.Stockpile && ui.StockpileMenu == StockpileSubmenu.None)
            return TryHandleStockpileRootClick(screenCell, screenWidth, screenHeight, ui);

        return false;
    }

    private static bool TryHandleOrdersRootClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui)
    {
        int x = (screenWidth - 30) / 2;
        int y = screenHeight - 9;
        if (!IsInsideMenuRows(screenCell, x, y, rowMax: 7))
            return false;

        switch (screenCell.Y - y)
        {
            case 1:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Mining);
                break;
            case 2:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Lumbering);
                break;
            case 3:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Gather);
                break;
            case 4:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Masonry);
                break;
            case 5:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Haul);
                break;
            case 6:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Creature);
                break;
            case 7:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Other);
                break;
        }

        return true;
    }

    private static bool TryHandleZonesRootClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui)
    {
        int x = (screenWidth - 30) / 2;
        int y = screenHeight - 9;
        if (!IsInsideMenuRows(screenCell, x, y, rowMax: 5))
            return false;

        switch (screenCell.Y - y)
        {
            case 1:
                ui.OpenZoneSubmenu(ZoneSubmenu.Production);
                break;
            case 2:
                ui.OpenZoneSubmenu(ZoneSubmenu.Civil);
                break;
            case 3:
                ui.OpenZoneSubmenu(ZoneSubmenu.Public);
                break;
            case 4:
                ui.OpenZoneSubmenu(ZoneSubmenu.Military);
                break;
            case 5:
                ui.OpenZoneSubmenu(ZoneSubmenu.Management);
                break;
        }

        return true;
    }

    private static bool TryHandleBuildRootClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui)
    {
        int x = (screenWidth - 30) / 2;
        int y = screenHeight - 9;
        if (!IsInsideMenuRows(screenCell, x, y, rowMax: 5))
            return false;

        switch (screenCell.Y - y)
        {
            case 1:
                ui.OpenBuildSubmenu(BuildSubmenu.Structural);
                break;
            case 2:
                ui.OpenBuildSubmenu(BuildSubmenu.FunctionalStructure);
                break;
            case 3:
                ui.OpenBuildSubmenu(BuildSubmenu.Workshop);
                break;
            case 4:
                ui.OpenBuildSubmenu(BuildSubmenu.CivilFurniture);
                break;
            case 5:
                ui.OpenBuildSubmenu(BuildSubmenu.UtilityFurniture);
                break;
        }

        return true;
    }

    private static bool TryHandleMiningClick(Point screenCell, int centerX, int screenHeight, UiStore ui, int currentZ, ulong uiTick)
    {
        int l3X = centerX + 2;
        int l3Y = screenHeight - 11;
        const int width = 28;
        const int height = 8;
        if (screenCell.X < l3X || screenCell.X >= l3X + width || screenCell.Y < l3Y || screenCell.Y >= l3Y + height)
            return false;

        int row = screenCell.Y - l3Y;
        if (row < 1 || row > 5)
            return false;

        ui.SelectedMiningAction = row switch
        {
            1 => MiningAction.Dig,
            2 => MiningAction.DigStairwell,
            3 => MiningAction.DigRamp,
            4 => MiningAction.DigChannel,
            5 => MiningAction.RemoveDigging,
            _ => ui.SelectedMiningAction
        };
        ui.StartPlacement(PlacementMode.MiningFirstCorner, currentZ);
        ui.AddToast("Mining: select first corner", uiTick + 120);
        return true;
    }

    private static bool TryHandleStockpileRootClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui)
    {
        int x = (screenWidth - 30) / 2;
        int y = screenHeight - 7;
        if (!IsInsideMenuRows(screenCell, x, y, rowMax: 3))
            return false;

        if (screenCell.Y - y == 1)
            ui.OpenStockpileSubmenu(StockpileSubmenu.Stockpile);

        return true;
    }

    private static bool IsInsideMenuRows(Point screenCell, int x, int y, int rowMax)
    {
        return screenCell.X >= x + 2
            && screenCell.X < x + 30
            && screenCell.Y >= y + 1
            && screenCell.Y <= y + rowMax;
    }
}
