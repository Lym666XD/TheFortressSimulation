using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static class FortressRightClickCancelInput
{
    public static bool Handle(
        Point screenCell,
        UiStore ui,
        bool tilePanelOpen,
        ZonesUI? zonesUI,
        StockpileUI? stockpileUI,
        Action hideTilePanel)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(hideTilePanel);

        Logger.Log($"[RIGHT-CLICK] Detected at screen=({screenCell.X},{screenCell.Y}), tilePanelOpen={tilePanelOpen}, QuickMenu={ui.QuickMenu}, OrdersMenu={ui.OrdersMenu}, ZoneMenu={ui.ZoneMenu}");

        if (tilePanelOpen)
        {
            Logger.Log("[RIGHT-CLICK] Closing tile panel");
            hideTilePanel();
            return true;
        }

        if (zonesUI?.IsDetailPopupOpen() == true)
        {
            Logger.Log("[RIGHT-CLICK] Closing zone detail popup");
            zonesUI.CloseDetailPopup();
            return true;
        }

        if (stockpileUI != null)
        {
            Logger.Log("[RIGHT-CLICK] Trying stockpile popup close");
            stockpileUI.CloseEditPopup();

            if (ui.QuickMenu != QuickMenuKind.None)
                return BackFromQuickMenu(ui, "[RIGHT-CLICK]");

            Logger.Log("[RIGHT-CLICK] General cancel (no QuickMenu)");
            ui.Cancel();
            return true;
        }

        if (ui.QuickMenu != QuickMenuKind.None)
            return BackFromQuickMenu(ui, "[RIGHT-CLICK] Priority 4:");

        Logger.Log("[RIGHT-CLICK] Priority 5: General cancel");
        ui.Cancel();
        return true;
    }

    private static bool BackFromQuickMenu(UiStore ui, string logPrefix)
    {
        if (ui.OrdersMenu != OrdersSubmenu.None)
        {
            Logger.Log($"{logPrefix} Closing OrdersMenu submenu");
            ui.CloseOrdersSubmenu();
            return true;
        }

        if (ui.ZoneMenu != ZoneSubmenu.None)
        {
            Logger.Log($"{logPrefix} Closing ZoneMenu submenu");
            ui.CloseZoneSubmenu();
            return true;
        }

        if (ui.BuildMenu != BuildSubmenu.None)
        {
            Logger.Log($"{logPrefix} Closing BuildMenu submenu");
            ui.CloseBuildSubmenu();
            return true;
        }

        if (ui.StockpileMenu != StockpileSubmenu.None)
        {
            Logger.Log($"{logPrefix} Closing StockpileMenu submenu");
            ui.CloseStockpileSubmenu();
            return true;
        }

        Logger.Log($"{logPrefix} Closing QuickMenu entirely (was in L2)");
        ui.CancelPlacement();
        return true;
    }
}
