using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

/// <summary>
/// Renders the Orders quick menu and haul submenu.
/// Minimal v1: shows keys Z→F→Z and status messages during placement.
/// </summary>
public sealed class OrdersUI
{
    public void DrawOrdersMenu(ScreenSurface surface, int x, int y)
    {
        var bg = Color.Black.SetAlpha(200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 22, 4, fg, bg);
        surface.Print(x + 1, y, " ORDERS ", highlight);
        surface.Print(x + 2, y + 1, "[F] Haul", fg);
        surface.Print(x + 2, y + 2, "[Z] Select tool", Color.Gray);
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

    public void DrawPlacementMode(ScreenSurface surface, UiStore ui, Point mouseWorld)
    {
        var statusY = surface.Height - 2;
        switch (ui.PlaceMode)
        {
            case PlacementMode.HaulFirstCorner:
                surface.Print(2, statusY, "[HAUL] Click first corner - ESC to cancel", Color.Yellow);
                break;
            case PlacementMode.HaulSecondCorner:
                if (ui.PlaceFirstCorner.HasValue)
                {
                    var size = (System.Math.Abs(mouseWorld.X - ui.PlaceFirstCorner.Value.X) + 1,
                                 System.Math.Abs(mouseWorld.Y - ui.PlaceFirstCorner.Value.Y) + 1);
                    surface.Print(2, statusY,
                        $"[HAUL] Click opposite corner - {size.Item1}x{size.Item2} tiles - ESC to cancel",
                        Color.Yellow);
                }
                break;
        }
    }

    private void DrawBox(ScreenSurface surface, int x, int y, int width, int height,
        Color fg, Color bg)
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

