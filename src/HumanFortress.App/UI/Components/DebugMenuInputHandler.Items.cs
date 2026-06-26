using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class DebugMenuInputHandler
{
    private bool TryHandleItemCategoryClick(
        UiStore ui,
        SimulationDebugMenuData debugMenu,
        Rectangle win,
        Point localPos)
    {
        string[] labels = DebugLayoutCalculator.GetCategoryLabels();
        var hits = DebugLayoutCalculator.CalculateCategoryPills(win, labels);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].Contains(localPos))
                continue;

            if (!DebugLayoutCalculator.TryGetCategoryByIndex(i, out var newCat))
                return true;

            ui.DebugItemCat = newCat;
            ui.DebugItemPage = 0;

            var ids = GetCategoryItemIds(debugMenu, newCat).ToList();
            if (ids.Count > 0) ui.DebugSelectedItem = ids[0];

            _addToast($"Category: {labels[i]}", 50);
            return true;
        }

        return false;
    }

    private bool TryHandleItemPageClick(
        UiStore ui,
        SimulationDebugMenuData debugMenu,
        Rectangle win,
        Point localPos)
    {
        var pageButtons = DebugLayoutCalculator.CalculatePageButtons(win);
        if (!pageButtons[0].Contains(localPos) && !pageButtons[1].Contains(localPos))
        {
            return false;
        }

        var idsAll = GetCategoryItemIds(debugMenu, ui.DebugItemCat).ToList();
        const int pageSize = 10;
        int maxPage = idsAll.Count > 0 ? (idsAll.Count - 1) / pageSize : 0;
        if (pageButtons[0].Contains(localPos)) ui.DebugItemPage = Math.Max(0, ui.DebugItemPage - 1);
        else ui.DebugItemPage = Math.Min(maxPage, ui.DebugItemPage + 1);

        int offset = ui.DebugItemPage * pageSize;
        if (idsAll.Count > offset) ui.DebugSelectedItem = idsAll[offset];
        _addToast($"Page {ui.DebugItemPage + 1}/{maxPage + 1}", 50);
        return true;
    }

    private bool TryHandleItemRowClick(
        UiStore ui,
        SimulationDebugMenuData debugMenu,
        Rectangle win,
        Point localPos)
    {
        var rowHits = DebugLayoutCalculator.CalculateItemRows(win, 10);
        var all = GetCategoryItemIds(debugMenu, ui.DebugItemCat).ToList();
        const int pageSize = 10;
        int offset = ui.DebugItemPage * pageSize;
        var list = all.Skip(offset).Take(10).ToList();
        for (int i = 0; i < rowHits.Length && i < list.Count; i++)
        {
            if (!rowHits[i].Contains(localPos))
                continue;

            ui.DebugSelectedItem = list[i];
            _addToast($"Selected: {list[i]}", 40);
            return true;
        }

        return false;
    }
}
