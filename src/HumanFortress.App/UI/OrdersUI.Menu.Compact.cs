using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class OrdersUI
{
    public void DrawOrdersMenu(ScreenSurface surface, int x, int y)
    {
        var bg = Color.Black.SetAlpha(200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 22, 4, fg, bg);
        surface.Print(x + 1, y, " ORDERS ", highlight);
        surface.Print(x + 2, y + 1, "[F] Haul", fg);
        surface.Print(x + 2, y + 2, "[M] Mining", fg);
        surface.Print(x + 2, y + 3, "[Z/D] Tool", Color.Gray);
    }

    public void DrawHaulMenu(ScreenSurface surface, int x, int y)
    {
        var bg = Color.Black.SetAlpha(200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 26, 5, fg, bg);
        surface.Print(x + 1, y, " HAUL ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Rect select", fg);
        surface.Print(x + 2, y + 2, "Right-Click: Cancel", Color.Gray);
        surface.Print(x + 2, y + 3, "ESC: Back", Color.Gray);
    }

    public void DrawMiningMenu(ScreenSurface surface, int x, int y)
    {
        var bg = Color.Black.SetAlpha(200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 26, 5, fg, bg);
        surface.Print(x + 1, y, " MINING ", highlight);
        surface.Print(x + 2, y + 1, "[D] Rect select", fg);
        surface.Print(x + 2, y + 2, "Right-Click: Cancel", Color.Gray);
        surface.Print(x + 2, y + 3, "ESC: Back", Color.Gray);
    }
}
