using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;
using System.Linq;

namespace HumanFortress.App.UI;

internal static partial class UiManagementDrawerRenderer
{
    private static void DrawItemsTab(ICellSurface surf, ItemDrawerData items, UiStore ui, int startY, int maxHeight)
    {
        IReadOnlyList<string> availableKinds = items.AvailableKinds.Count == 0 ? new[] { "all" } : items.AvailableKinds;
        surf.Print(2, startY, "Filter by kind: [", Color.Gray);
        int filterX = 18;
        foreach (var kind in availableKinds)
        {
            bool active = ui.ItemKindFilter == kind;
            var color = active ? Color.Yellow : Color.DarkGray;
            surf.Print(filterX, startY, kind, color);
            filterX += kind.Length + 1;
            if (kind != availableKinds[availableKinds.Count - 1])
                surf.Print(filterX - 1, startY, "|", Color.Gray);
        }

        var filteredItems = ui.ItemKindFilter == "all"
            ? items.GroundItems.ToList()
            : items.GroundItems
                .Where(item => string.Equals(item.Kind, ui.ItemKindFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        surf.Print(2, startY + 2, $"=== Items on Map ({filteredItems.Count}) ===", Color.Yellow);

        if (filteredItems.Count == 0)
        {
            surf.Print(2, startY + 4, "No items found.", Color.DarkGray);
            surf.Print(2, startY + 5, "Use F12 Debug menu to spawn items.", Color.DarkGray);
            return;
        }

        int y = startY + 4;
        int maxY = startY + maxHeight - 2;
        int displayed = 0;
        int totalUnits = filteredItems.Sum(item => item.StackCount);

        foreach (var item in filteredItems.Take(20))
        {
            if (y >= maxY) break;

            int qty = item.StackCount;
            bool selected = ui.SelectedItemGuid == item.ItemId.ToString();
            var bgColor = selected ? new Color(50, 50, 0) : new Color(20, 20, 20);

            for (int x = 2; x < surf.Width - 2; x++)
                surf.SetGlyph(x, y, ' ', Color.White, bgColor);

            string qtyStr = qty > 1 ? $"x{qty}" : "";
            surf.Print(2, y, $"{item.DisplayName,-15} {qtyStr,-4} @ ({item.X,3},{item.Y,3},{item.Z,2})", Color.White);

            y++;
            displayed++;
        }

        if (filteredItems.Count > displayed)
        {
            surf.Print(2, y, $"... and {filteredItems.Count - displayed} more", Color.DarkGray);
        }

        surf.Print(2, startY + maxHeight - 1, $"Total: {filteredItems.Count} items  {totalUnits} units", Color.Cyan);
    }
}
