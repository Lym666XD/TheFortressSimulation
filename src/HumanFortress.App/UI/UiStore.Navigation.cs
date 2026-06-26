namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    public UiContext Context { get; private set; } = UiContext.Global;
    public DrawerId OpenDrawer { get; private set; } = DrawerId.None;
    public int DrawerTab { get; private set; } = 0;
    public QuickMenuKind QuickMenu { get; private set; } = QuickMenuKind.None;

    public ZoneSubmenu ZoneMenu { get; private set; } = ZoneSubmenu.None;
    public OrdersSubmenu OrdersMenu { get; private set; } = OrdersSubmenu.None;
    public BuildSubmenu BuildMenu { get; private set; } = BuildSubmenu.None;
    public StockpileSubmenu StockpileMenu { get; private set; } = StockpileSubmenu.None;

    public void Back()
    {
        if (Context == UiContext.Drawer)
        {
            OpenDrawer = DrawerId.None;
            Context = QuickMenu != QuickMenuKind.None ? UiContext.QuickMenu : UiContext.Global;
        }
        else if (Context == UiContext.QuickMenu)
        {
            BackFromQuickMenu();
        }
        else if (Context == UiContext.PlacingTool)
        {
            CancelPlacement();
        }
        else if (HelpOpen)
        {
            HelpOpen = false;
        }
        else if (DebugOpen)
        {
            DebugOpen = false;
        }
    }

    public void Cancel()
    {
        if (Context == UiContext.PlacingTool || Context == UiContext.QuickMenu || Context == UiContext.Drawer)
        {
            CancelPlacement();
        }
        else if (HelpOpen)
        {
            HelpOpen = false;
        }
        else if (DebugOpen)
        {
            DebugOpen = false;
        }
        else if (WorkshopPanelOpen)
        {
            CloseWorkshopPanel();
        }
    }
}
