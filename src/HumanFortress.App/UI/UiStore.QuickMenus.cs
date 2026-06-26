namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    public void OpenQuickMenu(QuickMenuKind kind)
    {
        if (QuickMenu == kind)
        {
            QuickMenu = QuickMenuKind.None;
            ZoneMenu = ZoneSubmenu.None;
            BuildMenu = BuildSubmenu.None;
            ResetWorkshopMenu();
            if (OpenDrawer == DrawerId.None)
                Context = UiContext.Global;
            return;
        }

        ZoneMenu = ZoneSubmenu.None;
        BuildMenu = BuildSubmenu.None;
        ResetWorkshopMenu();
        QuickMenu = kind;
        Context = UiContext.QuickMenu;
    }

    public void OpenZoneSubmenu(ZoneSubmenu submenu)
    {
        ZoneMenu = submenu;
    }

    public void CloseZoneSubmenu()
    {
        ZoneMenu = ZoneSubmenu.None;
    }

    public void OpenOrdersSubmenu(OrdersSubmenu submenu)
    {
        OrdersMenu = submenu;
    }

    public void CloseOrdersSubmenu()
    {
        OrdersMenu = OrdersSubmenu.None;
    }

    public void OpenBuildSubmenu(BuildSubmenu submenu)
    {
        BuildMenu = submenu;
        if (submenu == BuildSubmenu.Workshop)
        {
            ResetWorkshopMenu();
        }
    }

    public void CloseBuildSubmenu()
    {
        BuildMenu = BuildSubmenu.None;
        ResetWorkshopMenu();
    }

    public void OpenStockpileSubmenu(StockpileSubmenu submenu)
    {
        StockpileMenu = submenu;
    }

    public void CloseStockpileSubmenu()
    {
        StockpileMenu = StockpileSubmenu.None;
    }
}
