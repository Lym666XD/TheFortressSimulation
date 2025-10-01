using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

/// <summary>
/// Renders the Stockpile quick menu (V button).
/// Migrated from Zones (X) stockpile functionality.
/// </summary>
public sealed class StockpileQuickUI
{
    public void DrawStockpileRootPopup(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 30, 6, fg, bg);
        surface.Print(x + 1, y, " STOCKPILES ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Stockpile", fg);
        surface.Print(x + 2, y + 2, "[X] Garbage Dump", fg);
        surface.Print(x + 2, y + 3, "[,] Remove Zone", fg);
        surface.Print(x + 2, y + 4, "ESC: Cancel", Color.Gray);
    }

    public void DrawStockpileWithSubmenu(ScreenSurface surface, int centerX, int centerY, StockpileSubmenu activeSubmenu)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        var activeBg = new Color(40, 40, 0, 220);

        // L2 menu (left side)
        int l2Width = 26;
        int l2Height = 6;
        int l2X = centerX - l2Width - 2;
        int l2Y = centerY;

        DrawBox(surface, l2X, l2Y, l2Width, l2Height, fg, bg);
        surface.Print(l2X + 1, l2Y, " STOCKPILES ", highlight);

        DrawMenuOption(surface, l2X + 2, l2Y + 1, "[Z] Stockpile", activeSubmenu == StockpileSubmenu.Stockpile, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 2, "[X] Garbage Dump", false, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 3, "[,] Remove Zone", false, fg, activeBg);
        surface.Print(l2X + 2, l2Y + 4, "ESC: Cancel", Color.Gray);

        // L3 menu (right side) - only for Stockpile submenu
        if (activeSubmenu == StockpileSubmenu.Stockpile)
        {
            int l3X = centerX + 2;
            int l3Y = centerY;

            DrawBox(surface, l3X, l3Y, 28, 5, fg, bg);
            surface.Print(l3X + 1, l3Y, " STOCKPILE ", highlight);
            surface.Print(l3X + 2, l3Y + 1, "[Z] create stockpile", fg);
            surface.Print(l3X + 2, l3Y + 2, "[,] remove stockpile", fg);
        }
    }

    private void DrawMenuOption(ScreenSurface surface, int x, int y, string text, bool active, Color fg, Color activeBg)
    {
        if (active)
        {
            for (int i = 0; i < 22; i++)
                surface.SetGlyph(x + i, y, ' ', fg, activeBg);
            surface.Print(x, y, text, Color.Yellow);
        }
        else
        {
            surface.Print(x, y, text, fg);
        }
    }

    private void DrawBox(ScreenSurface surface, int x, int y, int width, int height, Color fg, Color bg)
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                surface.SetGlyph(x + i, y + j, ' ', fg, bg);

        for (int i = 1; i < width - 1; i++)
        {
            surface.SetGlyph(x + i, y, '-', fg, bg);
            surface.SetGlyph(x + i, y + height - 1, '-', fg, bg);
        }
        for (int j = 1; j < height - 1; j++)
        {
            surface.SetGlyph(x, y + j, '|', fg, bg);
            surface.SetGlyph(x + width - 1, y + j, '|', fg, bg);
        }
        surface.SetGlyph(x, y, '+', fg, bg);
        surface.SetGlyph(x + width - 1, y, '+', fg, bg);
        surface.SetGlyph(x, y + height - 1, '+', fg, bg);
        surface.SetGlyph(x + width - 1, y + height - 1, '+', fg, bg);
    }
}
