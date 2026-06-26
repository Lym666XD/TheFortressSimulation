namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    private void BackFromQuickMenu()
    {
        if (OrdersMenu != OrdersSubmenu.None)
        {
            OrdersMenu = OrdersSubmenu.None;
        }
        else if (ZoneMenu != ZoneSubmenu.None)
        {
            ZoneMenu = ZoneSubmenu.None;
        }
        else if (BuildMenu != BuildSubmenu.None)
        {
            BuildMenu = BuildSubmenu.None;
        }
        else if (StockpileMenu != StockpileSubmenu.None)
        {
            StockpileMenu = StockpileSubmenu.None;
        }
        else
        {
            QuickMenu = QuickMenuKind.None;
            Context = UiContext.Global;
        }
    }
}
