using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiManagementDrawerRenderer
{
    private static void DrawDrawerBackground(ICellSurface surf, int y0)
    {
        for (int y = y0; y < surf.Height - 1; y++)
        {
            for (int x = 0; x < surf.Width; x++)
            {
                surf.SetGlyph(x, y, ' ', Color.White, new Color(20, 20, 20));
            }
        }
    }

    private static void DrawDrawerChrome(ICellSurface surf, UiStore ui, int y0)
    {
        surf.Print(1, y0, $"== {GetDrawerTitle(ui.OpenDrawer)} ==", Color.Yellow);

        int tx = 24;
        var tabs = GetDrawerTabs(ui.OpenDrawer);
        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = ui.DrawerTab == i;
            var fg = active ? Color.Black : Color.White;
            var bg = active ? Color.Yellow : new Color(50, 50, 50);
            WritePill(surf, ref tx, y0, tabs[i], fg, bg);
            tx += 1;
        }
    }

    private static void WritePill(ICellSurface surf, ref int x, int y, string text, Color fg, Color bg)
    {
        surf.SetGlyph(x, y, ' ', Color.White, bg);
        surf.Print(x + 1, y, text, fg);
        x += text.Length + 2;
        surf.SetGlyph(x - 1, y, ' ', Color.White, bg);
    }

    private static string GetDrawerTitle(DrawerId drawer)
    {
        return drawer switch
        {
            DrawerId.Creature => "Creature Management",
            DrawerId.Stock => "Stock/Items Management",
            DrawerId.Work => "Work Management",
            DrawerId.PlacementManagement => "Placement Management",
            DrawerId.Military => "Military Management",
            DrawerId.Country => "Country Management",
            DrawerId.World => "World Map / Diplomacy",
            DrawerId.Log => "Log / Messages / History",
            _ => "Panel"
        };
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
