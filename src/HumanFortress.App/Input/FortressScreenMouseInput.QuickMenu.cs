using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressScreenMouseInput
{
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
}
