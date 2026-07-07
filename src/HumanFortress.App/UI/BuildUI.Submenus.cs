using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class BuildUI
{
    private void DrawStructuralL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        DrawBox(surface, x, y, 26, 8, fg, bg);
        surface.Print(x + 1, y, " STRUCTURAL ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Wall", fg);
        surface.Print(x + 2, y + 2, "[X] Floor", fg);
        surface.Print(x + 2, y + 3, "[C] Ramp", fg);
        surface.Print(x + 2, y + 4, "[V] Stairs", fg);
        surface.Print(x + 2, y + 6, "[,] Cancel", Color.Gray);
    }

    private void DrawWorkshopMenu(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        DrawBox(surface, x, y, 46, 12, fg, bg);
        surface.Print(x + 1, y, " WORKSHOPS ", highlight);

        surface.Print(x + 2, y + 2, "[Z] Mining", fg);
        surface.Print(x + 2, y + 3, "[X] Industry", fg);
        surface.Print(x + 2, y + 4, "[C] Farming", fg);
        surface.Print(x + 2, y + 5, "[V] Lumbering", fg);
        surface.Print(x + 2, y + 6, "[F] Crafts", fg);
        surface.Print(x + 2, y + 8, "[,] Back   ESC Cancel", Color.Gray);
    }
}
