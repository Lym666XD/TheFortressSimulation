using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressScreenMouseInput
{
    private static bool TryHandleOrdersRootClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui)
    {
        int x = (screenWidth - 30) / 2;
        int y = screenHeight - 9;
        if (!IsInsideMenuRows(screenCell, x, y, rowMax: 7))
            return false;

        switch (screenCell.Y - y)
        {
            case 1:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Mining);
                break;
            case 2:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Lumbering);
                break;
            case 3:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Gather);
                break;
            case 4:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Masonry);
                break;
            case 5:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Haul);
                break;
            case 6:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Creature);
                break;
            case 7:
                ui.OpenOrdersSubmenu(OrdersSubmenu.Other);
                break;
        }

        return true;
    }
}
