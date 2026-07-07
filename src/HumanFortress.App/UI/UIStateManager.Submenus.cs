using HumanFortress.App;

namespace HumanFortress.App.UI;

internal sealed partial class UIStateManager
{
    /// <summary>
    /// Open submenu within current quick menu.
    /// </summary>
    public void OpenSubmenu(int submenuIndex)
    {
        Logger.Log($"[UIStateManager] OpenSubmenu: menu={_store.QuickMenu} index={submenuIndex}");

        switch (_store.QuickMenu)
        {
            case QuickMenuKind.Orders:
                if (submenuIndex >= 0 && submenuIndex <= 6)
                    _store.OpenOrdersSubmenu((OrdersSubmenu)(submenuIndex + 1));
                break;

            case QuickMenuKind.Zones:
                if (submenuIndex >= 0 && submenuIndex <= 4)
                    _store.OpenZoneSubmenu((ZoneSubmenu)(submenuIndex + 1));
                break;

            case QuickMenuKind.Build:
                if (submenuIndex >= 0 && submenuIndex <= 4)
                    _store.OpenBuildSubmenu((BuildSubmenu)(submenuIndex + 1));
                break;

            case QuickMenuKind.Stockpile:
                if (submenuIndex == 0)
                    _store.OpenStockpileSubmenu(StockpileSubmenu.Stockpile);
                break;
        }
    }
}
