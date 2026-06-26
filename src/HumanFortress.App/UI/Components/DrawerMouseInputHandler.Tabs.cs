using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class DrawerMouseInputHandler
{
    private bool TryHandleDrawerTabClick(Point localPos)
    {
        if (_uiStateManager.OpenDrawer == DrawerId.None)
        {
            return false;
        }

        int drawerTopY = CalculateDrawerTopY();
        string[] tabs = GetDrawerTabs(_uiStateManager.OpenDrawer);

        int? tabIndex = ButtonLayoutCalculator.HitTestDrawerTabs(localPos, _screenWidth, _screenHeight, tabs, drawerTopY);
        if (!tabIndex.HasValue || tabIndex.Value >= tabs.Length)
        {
            return false;
        }

        _uiStateManager.Store.SetDrawerTab(tabIndex.Value);
        Logger.Log($"[InputHandler] Drawer tab clicked: {tabs[tabIndex.Value]} (index {tabIndex.Value})");
        _uiStateManager.Store.SuppressNextTileClick = true;
        return true;
    }

    private static string[] GetDrawerTabs(DrawerId drawer)
    {
        return drawer switch
        {
            DrawerId.Creature => new[] { "All Creatures", "Animals", "Settings" },
            DrawerId.Stock => new[] { "Items", "Stockpiles", "Trade" },
            DrawerId.Work => new[] { "Labor", "All Orders", "Job Allocation", "Workshop Orders", "Workshops" },
            DrawerId.PlacementManagement => new[] { "Zones", "Stockpiles", "Settings" },
            _ => new[] { "Tab 1", "Tab 2", "Tab 3" }
        };
    }
}
