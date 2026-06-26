using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class ZonesUI
{
    private void DrawProductionL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 34, 9, fg, bg);
        surface.Print(x + 1, y, " PRODUCTION ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Lumbering", fg);
        surface.Print(x + 2, y + 2, "[X] Gather Plants", fg);
        surface.Print(x + 2, y + 3, "[C] Fishing", fg);
        surface.Print(x + 2, y + 4, "[V] Sand/Clay", fg);
        surface.Print(x + 2, y + 5, "[R] Pasture", fg);
        surface.Print(x + 2, y + 6, "[,] Remove Zone", fg);
    }

    private void DrawCivilL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 9, fg, bg);
        surface.Print(x + 1, y, " CIVIL ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Bedroom", fg);
        surface.Print(x + 2, y + 2, "[X] Dormitory", fg);
        surface.Print(x + 2, y + 3, "[C] Dining Hall", fg);
        surface.Print(x + 2, y + 4, "[V] Bathhouse", fg);
        surface.Print(x + 2, y + 5, "[G] Tomb", fg);
        surface.Print(x + 2, y + 6, "[,] Remove Zone", fg);
    }

    private void DrawPublicL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 32, 10, fg, bg);
        surface.Print(x + 1, y, " PUBLIC ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Assembly", fg);
        surface.Print(x + 2, y + 2, "[C] Temple", fg);
        surface.Print(x + 2, y + 3, "[V] Tavern/Inn", fg);
        surface.Print(x + 2, y + 4, "[F] Office", fg);
        surface.Print(x + 2, y + 5, "[G] Library", fg);
        surface.Print(x + 2, y + 6, "[T] Hospital", fg);
        surface.Print(x + 2, y + 7, "[,] Remove Zone", fg);
    }

    private void DrawMilitaryL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 6, fg, bg);
        surface.Print(x + 1, y, " MILITARY ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Military Grounds", fg);
        surface.Print(x + 2, y + 2, "[,] Remove Zone", fg);
    }

    private void DrawManagementL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 6, fg, bg);
        surface.Print(x + 1, y, " MANAGEMENT ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Burrow", fg);
        surface.Print(x + 2, y + 2, "[X] Restricted Traffic", fg);
        surface.Print(x + 2, y + 3, "[,] Remove Zone", fg);
    }
}
