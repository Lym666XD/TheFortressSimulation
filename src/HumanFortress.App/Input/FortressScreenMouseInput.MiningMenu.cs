using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressScreenMouseInput
{
    private static bool TryHandleMiningClick(Point screenCell, int centerX, int screenHeight, UiStore ui, int currentZ, ulong uiTick)
    {
        int l3X = centerX + 2;
        int l3Y = screenHeight - 11;
        const int width = 28;
        const int height = 8;
        if (screenCell.X < l3X || screenCell.X >= l3X + width || screenCell.Y < l3Y || screenCell.Y >= l3Y + height)
            return false;

        int row = screenCell.Y - l3Y;
        if (row < 1 || row > 5)
            return false;

        ui.SelectedMiningAction = row switch
        {
            1 => MiningAction.Dig,
            2 => MiningAction.DigStairwell,
            3 => MiningAction.DigRamp,
            4 => MiningAction.DigChannel,
            5 => MiningAction.RemoveDigging,
            _ => ui.SelectedMiningAction
        };
        ui.StartPlacement(PlacementMode.MiningFirstCorner, currentZ);
        ui.AddToast("Mining: select first corner", uiTick + 120);
        return true;
    }
}
