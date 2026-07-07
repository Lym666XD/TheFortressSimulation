using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;

namespace HumanFortress.App.UI;

internal static partial class UiQuickMenuRenderer
{
    private static void DrawOrdersMenu(ScreenSurface mapSurface, UiStore ui, OrdersUI ordersUI, int centerX)
    {
        var surf = mapSurface.Surface;
        if (ui.OrdersMenu == OrdersSubmenu.None)
        {
            int x = (surf.Width - 30) / 2;
            int y = surf.Height - 9;
            ordersUI.DrawOrdersRootPopup(mapSurface, x, y);
        }
        else
        {
            int l2Y = surf.Height - 11;
            ordersUI.DrawOrdersWithSubmenu(mapSurface, centerX, l2Y, ui.OrdersMenu);
        }
    }

    private static void DrawZonesMenu(ScreenSurface mapSurface, UiStore ui, ZonesUI zonesUI, int centerX)
    {
        var surf = mapSurface.Surface;
        if (ui.ZoneMenu == ZoneSubmenu.None)
        {
            int x = (surf.Width - 30) / 2;
            int y = surf.Height - 9;
            zonesUI.DrawZonesRootPopup(mapSurface, x, y);
        }
        else
        {
            int l2Y = surf.Height - 9;
            zonesUI.DrawZonesWithSubmenu(mapSurface, centerX, l2Y, ui.ZoneMenu);
        }
    }

    private static void DrawBuildMenu(
        ScreenSurface mapSurface,
        UiStore ui,
        BuildUI buildUI,
        int centerX,
        SimulationBuildCatalogData? buildCatalog)
    {
        var surf = mapSurface.Surface;
        if (ui.BuildMenu == BuildSubmenu.None)
        {
            int x = (surf.Width - 30) / 2;
            int y = surf.Height - 9;
            buildUI.DrawBuildRootPopup(mapSurface, x, y);
            return;
        }

        int l2Y = surf.Height - 9;
        buildUI.DrawBuildWithSubmenu(mapSurface, centerX, l2Y, ui.BuildMenu);

        if (ui.BuildMenu == BuildSubmenu.Workshop && ui.WorkshopBrowsingItems && ui.SelectedWorkshopCategory != null)
        {
            DrawWorkshopItemsPane(mapSurface, centerX + 32, l2Y, ui.SelectedWorkshopCategory, buildCatalog);
        }
    }

    private static void DrawStockpileMenu(ScreenSurface mapSurface, UiStore ui, StockpileQuickUI stockpileUI, int centerX)
    {
        var surf = mapSurface.Surface;
        if (ui.StockpileMenu == StockpileSubmenu.None)
        {
            int x = (surf.Width - 30) / 2;
            int y = surf.Height - 7;
            stockpileUI.DrawStockpileRootPopup(mapSurface, x, y);
        }
        else
        {
            int l2Y = surf.Height - 7;
            stockpileUI.DrawStockpileWithSubmenu(mapSurface, centerX, l2Y, ui.StockpileMenu);
        }
    }
}
