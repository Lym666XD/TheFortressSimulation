using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class OrdersUI
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

    public void DrawOrdersWithSubmenu(ScreenSurface surface, int centerX, int centerY, OrdersSubmenu activeSubmenu)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        var activeBg = new Color(40, 40, 0, 220);

        int l2Width = 26;
        int l2Height = 10;
        int l2X = centerX - l2Width - 2;
        int l2Y = centerY;

        DrawBox(surface, l2X, l2Y, l2Width, l2Height, fg, bg);
        surface.Print(l2X + 1, l2Y, " ORDERS ", highlight);

        DrawMenuOption(surface, l2X + 2, l2Y + 1, "[Z] Mining Order", activeSubmenu == OrdersSubmenu.Mining, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 2, "[X] Lumbering Order", activeSubmenu == OrdersSubmenu.Lumbering, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 3, "[C] Gather Order", activeSubmenu == OrdersSubmenu.Gather, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 4, "[V] Masonry Order", activeSubmenu == OrdersSubmenu.Masonry, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 5, "[F] Haul Order", activeSubmenu == OrdersSubmenu.Haul, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 6, "[B] Creature Order", activeSubmenu == OrdersSubmenu.Creature, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 7, "[G] Other Order", activeSubmenu == OrdersSubmenu.Other, fg, activeBg);
        surface.Print(l2X + 2, l2Y + 8, "ESC: Cancel", Color.Gray);

        int l3X = centerX + 2;
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

}
