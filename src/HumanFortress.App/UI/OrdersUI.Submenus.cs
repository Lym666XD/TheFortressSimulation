using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class OrdersUI
{
    private void DrawMiningL3(ScreenSurface surface, int x, int y)
    {
        DrawOrdersSubmenuPanel(surface, x, y, "MINING", 8, MiningL3Lines);
    }

    private void DrawLumberingL3(ScreenSurface surface, int x, int y)
    {
        DrawOrdersSubmenuPanel(surface, x, y, "LUMBERING", 5, LumberingL3Lines);
    }

    private void DrawGatherL3(ScreenSurface surface, int x, int y)
    {
        DrawOrdersSubmenuPanel(surface, x, y, "GATHER", 6, GatherL3Lines);
    }

    private void DrawMasonryL3(ScreenSurface surface, int x, int y)
    {
        DrawOrdersSubmenuPanel(surface, x, y, "MASONRY", 8, MasonryL3Lines);
    }

    private void DrawHaulL3(ScreenSurface surface, int x, int y)
    {
        DrawOrdersSubmenuPanel(surface, x, y, "HAUL", 6, HaulL3Lines);
    }

    private void DrawCreatureL3(ScreenSurface surface, int x, int y)
    {
        DrawOrdersSubmenuPanel(surface, x, y, "CREATURE", 8, CreatureL3Lines);
    }

    private void DrawOtherL3(ScreenSurface surface, int x, int y)
    {
        DrawOrdersSubmenuPanel(surface, x, y, "OTHER", 11, OtherL3Lines);
    }

    private void DrawOrdersSubmenuPanel(
        ScreenSurface surface,
        int x,
        int y,
        string title,
        int height,
        IReadOnlyList<string> lines)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, height, fg, bg);
        surface.Print(x + 1, y, $" {title} ", highlight);

        for (int i = 0; i < lines.Count; i++)
            surface.Print(x + 2, y + 1 + i, lines[i], fg);
    }
}
