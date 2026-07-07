using HumanFortress.App.UI.Placement;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressPlacementController
{
    public static bool TryHandleHaulSecondCornerClick(
        FortressPlacementControllerContext context,
        Point worldPos)
    {
        var ui = context.Ui;
        if (ui.PlaceFirstCorner.HasValue && worldPos != ui.PlaceFirstCorner.Value)
        {
            ui.PlaceSecondCorner = worldPos;

            var rect = FortressPlacementGeometry.ComputeRectInclusive(ui.PlaceFirstCorner.Value, ui.PlaceSecondCorner.Value);
            context.Runtime.QueueHaulOrder(rect, context.CurrentZ, priority: 50);
            ui.AddToast("Haul order created", context.UiTick + 120);
            Logger.Log($"[UI] Select first=({ui.PlaceFirstCorner.Value.X},{ui.PlaceFirstCorner.Value.Y},{context.CurrentZ}) second=({worldPos.X},{worldPos.Y},{context.CurrentZ}) -> rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
            ui.CancelPlacement();
            context.Redraw();
        }

        return true;
    }
}
