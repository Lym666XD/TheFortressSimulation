using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressScreenMouseInput
{
    private static bool TryHandleZonesRootClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui)
    {
        int x = (screenWidth - 30) / 2;
        int y = screenHeight - 9;
        if (!IsInsideMenuRows(screenCell, x, y, rowMax: 5))
            return false;

        switch (screenCell.Y - y)
        {
            case 1:
                ui.OpenZoneSubmenu(ZoneSubmenu.Production);
                break;
            case 2:
                ui.OpenZoneSubmenu(ZoneSubmenu.Civil);
                break;
            case 3:
                ui.OpenZoneSubmenu(ZoneSubmenu.Public);
                break;
            case 4:
                ui.OpenZoneSubmenu(ZoneSubmenu.Military);
                break;
            case 5:
                ui.OpenZoneSubmenu(ZoneSubmenu.Management);
                break;
        }

        return true;
    }
}
