using SadConsole;
using SadRogue.Primitives;
using System.Linq;

namespace HumanFortress.App.UI
{
    /// <summary>
    /// Post-render overlay for Debug Items tab to support paging without touching legacy renderer.
    /// Draws page indicator and paged item list over the original list area.
    /// </summary>
    public static class DebugPageOverlayRenderer
    {
        public static void PostDrawItemsPage(ScreenSurface overlay, UiStore ui, HumanFortress.Simulation.World.World? world)
        {
            if (!ui.DebugOpen || ui.DebugMenuTab != 2 || overlay == null || world == null) return;

            var surf = overlay.Surface;
            var win = DebugLayoutCalculator.CalculateWindow(surf.Width, surf.Height);

            // Geometry that matches UiRenderer.DrawDebug
            int listStartY = win.Y + 5;
            int listHeight = 10; // rows
            int listX = win.X + 4;
            int listW = System.Math.Max(1, win.Width - 8);

            // Clear existing list rows (overwrite with background spaces)
            var bg = new Color(15, 15, 15, 180);
            for (int i = 0; i < listHeight; i++)
            {
                int y = listStartY + i;
                for (int x = listX; x < listX + listW; x++)
                    surf.SetGlyph(x, y, ' ', Color.White, bg);
            }

            // Build data
            var ids = world.Items.GetAllDefinitions().Select(d => d.Id).ToList();
            var catIds = GetCategoryItemIds(world, ui.DebugItemCat).ToList();
            int pageSize = 10;
            int maxPage = catIds.Count > 0 ? (catIds.Count - 1) / pageSize : 0;
            if (ui.DebugItemPage < 0) ui.DebugItemPage = 0;
            if (ui.DebugItemPage > maxPage) ui.DebugItemPage = maxPage;

            // Draw page hint & prev/next buttons
            surf.Print(win.X + 2, win.Y + 4, "< Prev", Color.Gray);
            surf.Print(win.X + win.Width - 10, win.Y + 4, "Next >", Color.Gray);
            int cx = win.X + (win.Width / 2) - 6;
            surf.Print(cx, win.Y + 4, $"Page {ui.DebugItemPage + 1}/{maxPage + 1}", Color.DarkGray);

            // Draw page items
            var pageItems = catIds.Skip(ui.DebugItemPage * pageSize).Take(pageSize).ToList();
            int shown = 0;
            foreach (var id in pageItems)
            {
                bool sel = ui.DebugSelectedItem == id;
                var color = sel ? Color.White : Color.DarkGray;
                string label = GetItemNameWithMaterial(world, id);
                surf.Print(listX, listStartY + shown, $"{label}", color);
                shown++;
                if (shown >= listHeight) break;
            }
        }

        private static System.Collections.Generic.IEnumerable<string> GetCategoryItemIds(HumanFortress.Simulation.World.World world, DebugItemCategory cat)
        {
            var defs = world.Items.GetAllDefinitions();
            bool Prefix(string s, string p) => s.StartsWith(p);
            return cat switch
            {
                DebugItemCategory.Boulders => defs.Select(d => d.Id).Where(id => Prefix(id, "core_item_boulder_")).OrderBy(s => s),
                DebugItemCategory.Blocks => defs.Select(d => d.Id).Where(id => Prefix(id, "core_item_block_")).OrderBy(s => s),
                DebugItemCategory.Logs => defs.Select(d => d.Id).Where(id => Prefix(id, "core_item_log_")).OrderBy(s => s),
                DebugItemCategory.Planks => defs.Select(d => d.Id).Where(id => Prefix(id, "core_item_plank_")).OrderBy(s => s),
                DebugItemCategory.Tools => defs.Select(d => d.Id).Where(id => Prefix(id, "core_tool_")).OrderBy(s => s),
                DebugItemCategory.Weapons => defs.Select(d => d.Id).Where(id => Prefix(id, "core_weapon_")).OrderBy(s => s),
                _ => System.Linq.Enumerable.Empty<string>()
            };
        }

        private static string GetItemNameWithMaterial(HumanFortress.Simulation.World.World world, string id)
        {
            var def = world.Items.GetDefinition(id);
            if (def == null) return id;
            var baseName = string.IsNullOrWhiteSpace(def.Name) ? id : def.Name!;
            // Ensure generic resources show material explicitly
            if (!string.IsNullOrEmpty(def.FixedMaterial))
            {
                var low = baseName.ToLowerInvariant();
                if (low == "boulder" || low == "block" || low == "plank" || low == "log")
                {
                    var matNice = MaterialSuffixFriendly(def.FixedMaterial);
                    if (!string.IsNullOrEmpty(matNice))
                        return $"{matNice} {baseName}";
                }
            }
            return baseName;
        }

        private static string MaterialSuffixFriendly(string materialId)
        {
            try
            {
                var parts = materialId.Split('_');
                var last = parts[^1];
                return char.ToUpperInvariant(last[0]) + last.Substring(1).Replace('_', ' ');
            }
            catch { return materialId; }
        }
    }
}
