using SadRogue.Primitives;

namespace HumanFortress.App.UI;

/// <summary>
/// Manages UI state transitions and provides a clean API for UI commands.
/// </summary>
internal sealed partial class UIStateManager
{
    private readonly UiStore _store;

    public UIStateManager(UiStore store)
    {
        _store = store;
    }

    public UiContext Context => _store.Context;
    public DrawerId OpenDrawer => _store.OpenDrawer;
    public int DrawerTab => _store.DrawerTab;
    public QuickMenuKind QuickMenu => _store.QuickMenu;
    public OrdersSubmenu OrdersMenu => _store.OrdersMenu;
    public ZoneSubmenu ZoneMenu => _store.ZoneMenu;
    public BuildSubmenu BuildMenu => _store.BuildMenu;
    public StockpileSubmenu StockpileMenu => _store.StockpileMenu;
    public PlacementMode PlaceMode => _store.PlaceMode;
    public Point? HoverTile => _store.HoverTile;
    public UiStore Store => _store;
}
