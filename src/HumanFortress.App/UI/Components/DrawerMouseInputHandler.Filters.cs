using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class DrawerMouseInputHandler
{
    private static readonly string[] ItemKindFilters =
    {
        "all",
        "resource",
        "weapon",
        "armor",
        "tool",
        "container",
        "consumable",
        "placeable",
        "ammo",
        "siege_weapon"
    };

    private bool TryHandleStockItemFilterClick(Point localPos)
    {
        if (_uiStateManager.OpenDrawer != DrawerId.Stock || _uiStateManager.DrawerTab != 0)
        {
            return false;
        }

        int filterRowY = CalculateDrawerTopY() + 2;
        int? filterIndex = ButtonLayoutCalculator.HitTestItemKindFilterPills(
            localPos,
            _screenWidth,
            _screenHeight,
            ItemKindFilters,
            filterRowY);
        if (!filterIndex.HasValue || filterIndex.Value >= ItemKindFilters.Length)
        {
            return false;
        }

        string newFilter = ItemKindFilters[filterIndex.Value];
        _uiStateManager.Store.ItemKindFilter = newFilter;
        Logger.Log($"[InputHandler] F2 filter changed to: {newFilter}");
        _addToast($"Filter: {newFilter}", 50);
        _uiStateManager.Store.SuppressNextTileClick = true;
        return true;
    }
}
