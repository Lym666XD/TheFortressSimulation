using HumanFortress.App.UI;
using HumanFortress.App.UI.Placement;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressPlacementController
{
    public static bool TryHandleStockpileSecondCornerClick(
        FortressPlacementControllerContext context,
        Point worldPos)
    {
        var ui = context.Ui;
        if (ui.PlaceFirstCorner.HasValue && worldPos != ui.PlaceFirstCorner.Value)
        {
            ui.PlaceSecondCorner = worldPos;
            ui.PlaceMode = PlacementMode.StockpilePresetSelect;
            Logger.Log($"[STOCKPILE] Second corner at ({worldPos.X},{worldPos.Y},{context.CurrentZ})");
            context.Redraw();
        }

        return true;
    }

    public static bool TryHandleStockpileCopyClick(
        FortressPlacementControllerContext context,
        Point worldPos)
    {
        var ui = context.Ui;
        if (context.StockpileUi != null
            && context.StockpileUi.TryOpenStockpileAt(
                worldPos,
                context.Runtime.FindStockpileAt(worldPos, context.CurrentZ)))
        {
            ui.AddToast("Stockpile settings copied", context.UiTick + 150);
            ui.CancelPlacement();
            context.Redraw();
        }

        return true;
    }

    public static void CreateStockpile(FortressPlacementControllerContext context, string presetId)
    {
        var ui = context.Ui;
        if (!context.Runtime.GetWorldAvailabilityData().HasWorld || !ui.PlaceFirstCorner.HasValue || !ui.PlaceSecondCorner.HasValue)
            return;

        var rect = FortressPlacementGeometry.ComputeRectInclusive(ui.PlaceFirstCorner.Value, ui.PlaceSecondCorner.Value);

        context.Runtime.QueueCreateStockpile(rect, context.CurrentZ, presetId);

        int selectedCells = rect.Width * rect.Height;
        ui.AddToast($"Stockpile order queued ({selectedCells} tiles)", context.UiTick + 150);
        Logger.Log($"[STOCKPILE.UI] Enqueue preset={presetId} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) z={context.CurrentZ}");
    }
}
