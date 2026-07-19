using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;

namespace HumanFortress.App.UI;

internal static partial class UiQuickMenuRenderer
{
    public static void Draw(
        ScreenSurface mapSurface,
        UiStore ui,
        OrdersUI? ordersUI = null,
        ZonesUI? zonesUI = null,
        BuildUI? buildUI = null,
        StockpileQuickUI? stockpileUI = null,
        SimulationBuildCatalogData? buildCatalog = null,
        SimulationZoneCatalogData? zoneCatalog = null)
    {
        if (ui.QuickMenu == QuickMenuKind.None) return;
        var surf = mapSurface.Surface;
        int centerX = surf.Width / 2;

        if (ui.QuickMenu == QuickMenuKind.Orders && ordersUI != null)
        {
            DrawOrdersMenu(mapSurface, ui, ordersUI, centerX);
        }
        else if (ui.QuickMenu == QuickMenuKind.Zones && zonesUI != null)
        {
            DrawZonesMenu(
                mapSurface,
                ui,
                zonesUI,
                centerX,
                zoneCatalog ?? SimulationZoneCatalogData.Empty);
        }
        else if (ui.QuickMenu == QuickMenuKind.Build && buildUI != null)
        {
            DrawBuildMenu(mapSurface, ui, buildUI, centerX, buildCatalog);
        }
        else if (ui.QuickMenu == QuickMenuKind.Stockpile && stockpileUI != null)
        {
            DrawStockpileMenu(mapSurface, ui, stockpileUI, centerX);
        }
    }
}
