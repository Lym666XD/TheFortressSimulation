using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

/// <summary>
/// Renders the Zones quick menu with L2 and L3 submenus.
/// </summary>
public sealed class ZonesUI
{
    public void DrawZonesRootPopup(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 30, 8, fg, bg);
        surface.Print(x + 1, y, " ZONES ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Production Zone", fg);
        surface.Print(x + 2, y + 2, "[X] Civil Zone", fg);
        surface.Print(x + 2, y + 3, "[C] Public Zone", fg);
        surface.Print(x + 2, y + 4, "[V] Military Zone", fg);
        surface.Print(x + 2, y + 5, "[F] Management Zone", fg);
        surface.Print(x + 2, y + 6, "ESC: Cancel", Color.Gray);
    }

    public void DrawZonesWithSubmenu(ScreenSurface surface, int centerX, int centerY, ZoneSubmenu activeSubmenu)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        var activeBg = new Color(40, 40, 0, 220);

        // L2 menu (left side)
        int l2Width = 26;
        int l2Height = 8;
        int l2X = centerX - l2Width - 2;
        int l2Y = centerY;

        DrawBox(surface, l2X, l2Y, l2Width, l2Height, fg, bg);
        surface.Print(l2X + 1, l2Y, " ZONES ", highlight);

        DrawMenuOption(surface, l2X + 2, l2Y + 1, "[Z] Production Zone", activeSubmenu == ZoneSubmenu.Production, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 2, "[X] Civil Zone", activeSubmenu == ZoneSubmenu.Civil, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 3, "[C] Public Zone", activeSubmenu == ZoneSubmenu.Public, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 4, "[V] Military Zone", activeSubmenu == ZoneSubmenu.Military, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 5, "[F] Management Zone", activeSubmenu == ZoneSubmenu.Management, fg, activeBg);
        surface.Print(l2X + 2, l2Y + 6, "ESC: Cancel", Color.Gray);

        // L3 menu (right side)
        int l3X = centerX + 2;
        int l3Y = centerY;

        switch (activeSubmenu)
        {
            case ZoneSubmenu.Production:
                DrawProductionL3(surface, l3X, l3Y);
                break;
            case ZoneSubmenu.Civil:
                DrawCivilL3(surface, l3X, l3Y);
                break;
            case ZoneSubmenu.Public:
                DrawPublicL3(surface, l3X, l3Y);
                break;
            case ZoneSubmenu.Military:
                DrawMilitaryL3(surface, l3X, l3Y);
                break;
            case ZoneSubmenu.Management:
                DrawManagementL3(surface, l3X, l3Y);
                break;
        }
    }

    private void DrawProductionL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 34, 11, fg, bg);
        surface.Print(x + 1, y, " PRODUCTION ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] lumbering zone", fg);
        surface.Print(x + 2, y + 2, "[X] gathering fruit/veg zone", fg);
        surface.Print(x + 2, y + 3, "[C] fishing zone", fg);
        surface.Print(x + 2, y + 4, "[V] gathering sand/clay zone", fg);
        surface.Print(x + 2, y + 5, "[F] water zone", fg);
        surface.Print(x + 2, y + 6, "[G] pit/pond", fg);
        surface.Print(x + 2, y + 7, "[R] pen/pasture/animal train", fg);
        surface.Print(x + 2, y + 8, "[,] remove zone", fg);
    }

    private void DrawCivilL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 10, fg, bg);
        surface.Print(x + 1, y, " CIVIL ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] bedroom", fg);
        surface.Print(x + 2, y + 2, "[X] dormitory", fg);
        surface.Print(x + 2, y + 3, "[C] dining hall", fg);
        surface.Print(x + 2, y + 4, "[V] bathroom", fg);
        surface.Print(x + 2, y + 5, "[F] shower room", fg);
        surface.Print(x + 2, y + 6, "[G] tomb", fg);
        surface.Print(x + 2, y + 7, "[,] remove zone", fg);
    }

    private void DrawPublicL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 32, 12, fg, bg);
        surface.Print(x + 1, y, " PUBLIC ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] throne/parliament room", fg);
        surface.Print(x + 2, y + 2, "[X] plaza", fg);
        surface.Print(x + 2, y + 3, "[C] temple", fg);
        surface.Print(x + 2, y + 4, "[V] tavern/inn", fg);
        surface.Print(x + 2, y + 5, "[F] office", fg);
        surface.Print(x + 2, y + 6, "[G] library", fg);
        surface.Print(x + 2, y + 7, "[R] guildhall", fg);
        surface.Print(x + 2, y + 8, "[T] hospital", fg);
        surface.Print(x + 2, y + 9, "[,] remove zone", fg);
    }

    private void DrawMilitaryL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 8, fg, bg);
        surface.Print(x + 1, y, " MILITARY ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] barracks", fg);
        surface.Print(x + 2, y + 2, "[X] archery range", fg);
        surface.Print(x + 2, y + 3, "[C] chivalry training", fg);
        surface.Print(x + 2, y + 4, "[V] arena", fg);
        surface.Print(x + 2, y + 5, "[,] remove zone", fg);
    }

    private void DrawManagementL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 6, fg, bg);
        surface.Print(x + 1, y, " MANAGEMENT ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] burrow zone", fg);
        surface.Print(x + 2, y + 2, "[X] banning traffic area", fg);
        surface.Print(x + 2, y + 3, "[,] remove zone", fg);
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
