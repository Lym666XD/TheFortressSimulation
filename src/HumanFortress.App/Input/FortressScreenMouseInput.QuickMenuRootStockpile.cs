using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressScreenMouseInput
{
    private static bool TryHandleStockpileRootClick(Point screenCell, int screenWidth, int screenHeight, UiStore ui)
    {
        int x = (screenWidth - 30) / 2;
        int y = screenHeight - 7;
        if (!IsInsideMenuRows(screenCell, x, y, rowMax: 3))
            return false;

        if (screenCell.Y - y == 1)
            ui.OpenStockpileSubmenu(StockpileSubmenu.Stockpile);

        return true;
    }
}
