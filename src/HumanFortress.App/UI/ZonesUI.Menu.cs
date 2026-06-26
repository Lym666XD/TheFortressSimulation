using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class ZonesUI
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
}
