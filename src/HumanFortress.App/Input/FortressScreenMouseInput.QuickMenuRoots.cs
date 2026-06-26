using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressScreenMouseInput
{
    private static bool IsInsideMenuRows(Point screenCell, int x, int y, int rowMax)
    {
        return screenCell.X >= x + 2
            && screenCell.X < x + 30
            && screenCell.Y >= y + 1
            && screenCell.Y <= y + rowMax;
    }
}
