using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private static void RenderStandingOrdersColumn(ICellSurface surf, Rectangle area)
    {
        surf.Print(area.X + 1, area.Y, "Standing Orders", Color.Yellow);
        int line = area.Y + 2;
        var toggles = new (string Label, string Value)[]
        {
            ("Auto-haul refuse", "Enabled (placeholder)"),
            ("Auto-weave cloth", "Enabled (placeholder)"),
            ("Kitchen cooking", "Allow seeds (TODO)"),
            ("Stone use", "All stone (TODO)")
        };
        foreach (var toggle in toggles)
        {
            if (line >= area.Y + area.Height - 1) break;
            surf.Print(area.X + 1, line++, toggle.Label, Color.White);
            surf.Print(area.X + 3, line++, toggle.Value, Color.Gray);
        }
        surf.Print(area.X + 1, area.Y + area.Height - 2, "TODO: Wire to standing-order data", Color.DarkGray);
    }
}
