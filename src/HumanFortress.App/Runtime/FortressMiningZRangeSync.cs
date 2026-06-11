using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;

namespace HumanFortress.App.Runtime;

internal static class FortressMiningZRangeSync
{
    public static void ApplyCurrentZ(UiStore ui, ISelectionTool? selectionTool, int currentZ)
    {
        ArgumentNullException.ThrowIfNull(ui);

        if (ui.PlaceMode != PlacementMode.MiningSecondCorner)
            return;

        if (selectionTool != null && selectionTool.IsActive)
        {
            selectionTool.SetZRangeEnd(currentZ);
            Logger.Log($"[ZLEVEL-UPDATE] Mining mode: updated PlaceZMax={currentZ} and selectionTool z-range");
        }
        else
        {
            ui.PlaceZMax = currentZ;
            Logger.Log($"[ZLEVEL-UPDATE] Mining mode: updated PlaceZMax={currentZ}");
        }
    }
}
