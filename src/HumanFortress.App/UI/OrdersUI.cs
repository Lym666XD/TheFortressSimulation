using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

/// <summary>
/// Renders the Orders quick menu and haul submenu.
/// Minimal v1: shows keys Z→F→Z and status messages during placement.
/// </summary>
public sealed class OrdersUI
{
    public void DrawOrdersRootPopup(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 30, 8, fg, bg);
        surface.Print(x + 1, y, " ORDERS ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Mining Order", fg);
        surface.Print(x + 2, y + 2, "[X] Lumbering Order", fg);
        surface.Print(x + 2, y + 3, "[C] Gathering Order", fg);
        surface.Print(x + 2, y + 4, "[V] Masonry Order", fg);
        surface.Print(x + 2, y + 5, "[F] Haul Order", fg);
        surface.Print(x + 2, y + 6, "ESC: Cancel", Color.Gray);
    }

    // Draw L2 + L3 side-by-side: L2 on left (highlighted active), L3 on right
    public void DrawOrdersWithSubmenu(ScreenSurface surface, int centerX, int centerY, OrdersSubmenu activeSubmenu)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        var activeBg = new Color(40, 40, 0, 220);

        // L2 menu (left side, shift left)
        int l2Width = 26;
        int l2Height = 10;
        int l2X = centerX - l2Width - 2; // shift left
        int l2Y = centerY;

        DrawBox(surface, l2X, l2Y, l2Width, l2Height, fg, bg);
        surface.Print(l2X + 1, l2Y, " ORDERS ", highlight);

        // Highlight active submenu in L2
        DrawMenuOption(surface, l2X + 2, l2Y + 1, "[Z] Mining Order", activeSubmenu == OrdersSubmenu.Mining, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 2, "[X] Lumbering Order", activeSubmenu == OrdersSubmenu.Lumbering, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 3, "[C] Gather Order", activeSubmenu == OrdersSubmenu.Gather, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 4, "[V] Masonry Order", activeSubmenu == OrdersSubmenu.Masonry, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 5, "[F] Haul Order", activeSubmenu == OrdersSubmenu.Haul, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 6, "[B] Creature Order", activeSubmenu == OrdersSubmenu.Creature, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 7, "[G] Other Order", activeSubmenu == OrdersSubmenu.Other, fg, activeBg);
        surface.Print(l2X + 2, l2Y + 8, "ESC: Cancel", Color.Gray);

        // L3 menu (right side) - draw specific submenu
        int l3X = centerX + 2; // shift right
        int l3Y = centerY;

        switch (activeSubmenu)
        {
            case OrdersSubmenu.Mining:
                DrawMiningL3(surface, l3X, l3Y);
                break;
            case OrdersSubmenu.Lumbering:
                DrawLumberingL3(surface, l3X, l3Y);
                break;
            case OrdersSubmenu.Gather:
                DrawGatherL3(surface, l3X, l3Y);
                break;
            case OrdersSubmenu.Masonry:
                DrawMasonryL3(surface, l3X, l3Y);
                break;
            case OrdersSubmenu.Haul:
                DrawHaulL3(surface, l3X, l3Y);
                break;
            case OrdersSubmenu.Creature:
                DrawCreatureL3(surface, l3X, l3Y);
                break;
            case OrdersSubmenu.Other:
                DrawOtherL3(surface, l3X, l3Y);
                break;
        }
    }

    private void DrawMiningL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 8, fg, bg);
        surface.Print(x + 1, y, " MINING ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Dig", fg);
        surface.Print(x + 2, y + 2, "[X] dig stairwell", fg);
        surface.Print(x + 2, y + 3, "[C] dig ramp", fg);
        surface.Print(x + 2, y + 4, "[V] dig channel", fg);
        surface.Print(x + 2, y + 5, "[F] remove digging", fg);
        surface.Print(x + 2, y + 6, "[,] cancel order", fg);
    }

    private void DrawLumberingL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 5, fg, bg);
        surface.Print(x + 1, y, " LUMBERING ", highlight);
        surface.Print(x + 2, y + 1, "[Z] lumber", fg);
        surface.Print(x + 2, y + 2, "[,] cancel order", fg);
    }

    private void DrawGatherL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 6, fg, bg);
        surface.Print(x + 1, y, " GATHER ", highlight);
        surface.Print(x + 2, y + 1, "[Z] gather plant", fg);
        surface.Print(x + 2, y + 2, "[X] remove plant", fg);
        surface.Print(x + 2, y + 3, "[,] cancel order", fg);
    }

    private void DrawMasonryL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 8, fg, bg);
        surface.Print(x + 1, y, " MASONRY ", highlight);
        surface.Print(x + 2, y + 1, "[Z] smooth", fg);
        surface.Print(x + 2, y + 2, "[X] engrave", fg);
        surface.Print(x + 2, y + 3, "[C] track", fg);
        surface.Print(x + 2, y + 4, "[V] carve gap", fg);
        surface.Print(x + 2, y + 5, "[,] cancel order", fg);
    }

    private void DrawHaulL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 6, fg, bg);
        surface.Print(x + 1, y, " HAUL ", highlight);
        surface.Print(x + 2, y + 1, "[Z] haul", fg);
        surface.Print(x + 2, y + 2, "[X] emergency haul", fg);
        surface.Print(x + 2, y + 3, "[,] cancel order", fg);
    }

    private void DrawCreatureL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 8, fg, bg);
        surface.Print(x + 1, y, " CREATURE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] hunting", fg);
        surface.Print(x + 2, y + 2, "[X] kill", fg);
        surface.Print(x + 2, y + 3, "[C] tame", fg);
        surface.Print(x + 2, y + 4, "[V] rescue", fg);
        surface.Print(x + 2, y + 5, "[,] cancel order", fg);
    }

    private void DrawOtherL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 11, fg, bg);
        surface.Print(x + 1, y, " OTHER ", highlight);
        surface.Print(x + 2, y + 1, "[Z] lock/disallow", fg);
        surface.Print(x + 2, y + 2, "[X] unlock/allow", fg);
        surface.Print(x + 2, y + 3, "[C] dump", fg);
        surface.Print(x + 2, y + 4, "[V] remove dump", fg);
        surface.Print(x + 2, y + 5, "[F] melt", fg);
        surface.Print(x + 2, y + 6, "[T] remove melt", fg);
        surface.Print(x + 2, y + 7, "[R] clean", fg);
        surface.Print(x + 2, y + 8, "[,] cancel order", fg);
    }

    private void DrawMenuOption(ScreenSurface surface, int x, int y, string text, bool active, Color fg, Color activeBg)
    {
        if (active)
        {
            // Draw background for active item
            for (int i = 0; i < 22; i++)
                surface.SetGlyph(x + i, y, ' ', fg, activeBg);
            surface.Print(x, y, text, Color.Yellow);
        }
        else
        {
            surface.Print(x, y, text, fg);
        }
    }

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
            case PlacementMode.MiningFirstCorner:
                surface.Print(2, statusY, $"[MINING] Click first corner  Z-range: {ui.PlaceZMin}..{ui.PlaceZMax} - ESC to cancel", Color.Cyan);
                break;
            case PlacementMode.MiningSecondCorner:
                if (ui.PlaceFirstCorner.HasValue)
                {
                    var size = (System.Math.Abs(mouseWorld.X - ui.PlaceFirstCorner.Value.X) + 1,
                                 System.Math.Abs(mouseWorld.Y - ui.PlaceFirstCorner.Value.Y) + 1);
                    surface.Print(2, statusY,
                        $"[MINING] Click opposite corner - {size.Item1}x{size.Item2} tiles  Z-range: {ui.PlaceZMin}..{ui.PlaceZMax} - ESC to cancel",
                        Color.Cyan);
                }
                break;
                case PlacementMode.ConstructionFirstCorner:
                    surface.Print(2, statusY, "[BUILD] Click first corner - ESC to cancel", Color.Yellow);
                    break;
                case PlacementMode.ConstructionSecondCorner:
                    if (ui.PlaceFirstCorner.HasValue)
                    {
                        var size = (System.Math.Abs(mouseWorld.X - ui.PlaceFirstCorner.Value.X) + 1,
                                     System.Math.Abs(mouseWorld.Y - ui.PlaceFirstCorner.Value.Y) + 1);
                        surface.Print(2, statusY,
                            $"[BUILD] Click opposite corner - {size.Item1}x{size.Item2} tiles - ESC to cancel",
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
