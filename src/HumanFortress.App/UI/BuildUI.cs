using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

/// <summary>
/// Renders the Build quick menu with L2 submenus (no L3 for now).
/// </summary>
public sealed class BuildUI
{
    public void DrawBuildRootPopup(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 30, 8, fg, bg);
        surface.Print(x + 1, y, " CONSTRUCTION ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Structural", fg);
        surface.Print(x + 2, y + 2, "[X] Functional Structure", fg);
        surface.Print(x + 2, y + 3, "[C] Workshop", fg);
        surface.Print(x + 2, y + 4, "[V] Civil Furniture", fg);
        surface.Print(x + 2, y + 5, "[F] Utility Furniture", fg);
        surface.Print(x + 2, y + 6, "ESC: Cancel", Color.Gray);
    }

    // For now, L2 menus are displayed without L3 details
    // In the future, workshops will have L3 submenus
    public void DrawBuildWithSubmenu(ScreenSurface surface, int centerX, int centerY, BuildSubmenu activeSubmenu)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        var activeBg = new Color(40, 40, 0, 220);

        // L2 menu (centered)
        int l2Width = 30;
        int l2Height = 8;
        int l2X = centerX - l2Width / 2;
        int l2Y = centerY;

        DrawBox(surface, l2X, l2Y, l2Width, l2Height, fg, bg);
        surface.Print(l2X + 1, l2Y, " CONSTRUCTION ", highlight);

        DrawMenuOption(surface, l2X + 2, l2Y + 1, "[Z] Structural", activeSubmenu == BuildSubmenu.Structural, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 2, "[X] Functional Structure", activeSubmenu == BuildSubmenu.FunctionalStructure, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 3, "[C] Workshop", activeSubmenu == BuildSubmenu.Workshop, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 4, "[V] Civil Furniture", activeSubmenu == BuildSubmenu.CivilFurniture, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 5, "[F] Utility Furniture", activeSubmenu == BuildSubmenu.UtilityFurniture, fg, activeBg);
        surface.Print(l2X + 2, l2Y + 6, "ESC: Cancel", Color.Gray);

        // L3 menu for active submenu
        if (activeSubmenu == BuildSubmenu.Structural)
        {
            DrawStructuralL3(surface, l2X + l2Width + 2, l2Y);
        }
    }

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

    private void DrawMenuOption(ScreenSurface surface, int x, int y, string text, bool active, Color fg, Color activeBg)
    {
        if (active)
        {
            for (int i = 0; i < 26; i++)
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
