using HumanFortress.App.UI;
using HumanFortress.App.UI.Placement;
using HumanFortress.App.UI.Selection;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal readonly record struct FortressMiningPlacementContext(
    UiStore Ui,
    FortressPlacementRuntimePorts Runtime,
    ISelectionTool? SelectionTool,
    int FortressSize,
    int CurrentZ,
    ulong UiTick,
    Action Redraw);

internal static class FortressMiningPlacementController
{
    public static bool TryHandleSecondCornerClick(
        FortressMiningPlacementContext context,
        Point worldPos)
    {
        var ui = context.Ui;
        var clampedWorldPos = FortressPlacementGeometry.ClampToWorld(worldPos, context.FortressSize);
        if (ui.PlaceFirstCorner.HasValue && clampedWorldPos != ui.PlaceFirstCorner.Value)
        {
            ui.PlaceSecondCorner = clampedWorldPos;
            ui.PlaceZMax = context.CurrentZ;

            Selection3D selection = default;
            if (context.SelectionTool != null && context.SelectionTool.IsActive)
            {
                selection = context.SelectionTool.Complete();
            }

            bool useSelectionTool = selection.XY.Width > 1 || selection.XY.Height > 1;
            var rect = useSelectionTool
                ? selection.XY
                : FortressPlacementGeometry.ComputeRectInclusive(ui.PlaceFirstCorner.Value, ui.PlaceSecondCorner.Value);

            int zMin = useSelectionTool ? Math.Min(selection.ZMin, selection.ZMax) : Math.Min(ui.PlaceZMin, ui.PlaceZMax);
            int zMax = useSelectionTool ? Math.Max(selection.ZMin, selection.ZMax) : Math.Max(ui.PlaceZMin, ui.PlaceZMax);
            var uiAction = ui.SelectedMiningAction;
            var runtimeAction = FortressPlacementRequestFactory.ToRuntimeMiningAction(uiAction);

            if (uiAction == MiningAction.DigStairwell && zMin == zMax)
            {
                ui.AddToast("Stairwell must dig between multiple levels", context.UiTick + 180);
                Logger.Log($"[UI] Single-layer stairwell rejected at UI (z={zMin})");
            }

            Logger.Log($"[DEBUG] Creating mining order command zMin={zMin} zMax={zMax} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
            context.Runtime.QueueAdvancedMiningOrder(
                rect,
                zMin,
                zMax,
                runtimeAction,
                priority: 50);

            int totalCells = rect.Width * rect.Height;
            ui.AddToast($"Mining order created ({totalCells} tiles)", context.UiTick + 120);
            Logger.Log($"[UI] Select first=({ui.PlaceFirstCorner.Value.X},{ui.PlaceFirstCorner.Value.Y},{context.CurrentZ}) second=({worldPos.X},{worldPos.Y},{context.CurrentZ}) -> rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
            Logger.Log($"[MINING] UI enqueued action={runtimeAction} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) z={zMin}..{zMax}");
            ui.AddHighlight($"mining:{uiAction}", rect, zMin, zMax, context.UiTick + 30);
            ui.CancelPlacement();
            context.Redraw();
        }

        return true;
    }
}
