using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressScreenMouseInput
{
    private static bool TryHandleBuildRootClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui)
    {
        int x = (screenWidth - 30) / 2;
        int y = screenHeight - 9;
        if (!IsInsideMenuRows(screenCell, x, y, rowMax: 5))
            return false;

        switch (screenCell.Y - y)
        {
            case 1:
                ui.OpenBuildSubmenu(BuildSubmenu.Structural);
                break;
            case 2:
                ui.OpenBuildSubmenu(BuildSubmenu.FunctionalStructure);
                break;
            case 3:
                ui.OpenBuildSubmenu(BuildSubmenu.Workshop);
                break;
            case 4:
                ui.OpenBuildSubmenu(BuildSubmenu.CivilFurniture);
                break;
            case 5:
                ui.OpenBuildSubmenu(BuildSubmenu.UtilityFurniture);
                break;
        }

        return true;
    }
}
