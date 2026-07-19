using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;
using System.Linq;

namespace HumanFortress.App.UI;

internal static partial class UiDebugMenuRenderer
{
    private static void DrawItemsTab(ICellSurface surf, int x0, int y0, int width, UiStore ui, SimulationDebugMenuData debugMenu)
    {
        surf.Print(x0 + 2, y0 + 2, "Spawn Item:", Color.Yellow);
        int catX = x0 + 2;
        foreach (var option in DebugLayoutCalculator.GetCategoryOptions())
        {
            bool active = ui.DebugItemCat == option.Category;
            WritePill(surf, ref catX, y0 + 3, option.Label, active ? Color.Black : Color.White, active ? Color.Yellow : new Color(40, 40, 40));
        }

        var itemRows = GetDebugItemsForCategory(debugMenu, ui.DebugItemCat);
        const int pageSize = 10;
        int maxPage = itemRows.Count > 0 ? (itemRows.Count - 1) / pageSize : 0;
        ui.DebugItemPage = Math.Clamp(ui.DebugItemPage, 0, maxPage);

        surf.Print(x0 + 2, y0 + 4, "< Prev", Color.Gray);
        surf.Print(x0 + width - 10, y0 + 4, "Next >", Color.Gray);
        int pageX = x0 + (width / 2) - 6;
        surf.Print(pageX, y0 + 4, $"Page {ui.DebugItemPage + 1}/{maxPage + 1}", Color.DarkGray);

        int listY = y0 + 5;
        foreach (var item in itemRows.Skip(ui.DebugItemPage * pageSize).Take(pageSize))
        {
            bool selected = ui.DebugSelectedItem == item.Id;
            var color = selected ? Color.White : Color.DarkGray;
            surf.Print(x0 + 4, listY, item.DisplayName, color);
            listY++;
        }

        surf.Print(x0 + 2, listY + 1, $"Selected: {GetItemNameOrId(debugMenu, ui.DebugSelectedItem)}", Color.Cyan);
        surf.Print(x0 + 2, listY + 3, "Click map to spawn at mouse position", Color.Green);
    }

    private static string GetItemNameOrId(SimulationDebugMenuData debugMenu, string id)
    {
        var item = debugMenu.ItemCategories?
            .SelectMany(category => category.Items ?? Array.Empty<DebugItemView>())
            .FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal)) ?? default;
        if (!string.IsNullOrWhiteSpace(item.Id))
            return item.DisplayName;

        return id;
    }

    private static IReadOnlyList<DebugItemView> GetDebugItemsForCategory(SimulationDebugMenuData debugMenu, DebugItemCategory category)
    {
        var categoryId = category.ToString();
        var debugCategory = debugMenu.ItemCategories?
            .FirstOrDefault(candidate => string.Equals(candidate.CategoryId, categoryId, StringComparison.Ordinal)) ?? default;
        return string.IsNullOrWhiteSpace(debugCategory.CategoryId)
            ? Array.Empty<DebugItemView>()
            : debugCategory.Items ?? Array.Empty<DebugItemView>();
    }
}
