using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class StockpileUI
{
    /// <summary>
    /// Draw zone menu (after X -> Z).
    /// </summary>
    public void DrawZoneMenu(ScreenSurface surface, int x, int y)
    {
        var bg = Color.Black.SetAlpha(200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 20, 4, fg, bg);
        surface.Print(x + 1, y, " ZONES ", highlight);

        surface.Print(x + 2, y + 1, "[Z] Stockpile", fg);
        surface.Print(x + 2, y + 2, "[R] Room", Color.Gray);
    }

    /// <summary>
    /// Draw stockpile submenu (after X -> Z -> Z).
    /// </summary>
    public void DrawStockpileMenu(ScreenSurface surface, int x, int y)
    {
        var bg = Color.Black.SetAlpha(200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 25, 5, fg, bg);
        surface.Print(x + 1, y, " STOCKPILE ", highlight);

        surface.Print(x + 2, y + 1, "[Z] Create new", fg);
        surface.Print(x + 2, y + 2, "[,] Delete area", fg);
        surface.Print(x + 2, y + 3, "[X] Copy settings", fg);
    }
}
