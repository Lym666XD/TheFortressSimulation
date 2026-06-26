using HumanFortress.App.Runtime;
using HumanFortress.App.UI;
using HumanFortress.App.UI.Placement;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal readonly record struct FortressConstructionPlacementContext(
    UiStore Ui,
    IFortressRuntimePlacementAccess Runtime,
    int FortressSize,
    int CurrentZ,
    ulong UiTick,
    Action Redraw);

internal static class FortressConstructionPlacementController
{
    public static bool TryHandleSecondCornerClick(
        FortressConstructionPlacementContext context,
        Point worldPos)
    {
        var ui = context.Ui;
        var clampedWorldPos = FortressPlacementGeometry.ClampToWorld(worldPos, context.FortressSize);
        if (!ui.PlaceFirstCorner.HasValue || clampedWorldPos == ui.PlaceFirstCorner.Value)
            return true;

        var rect = FortressPlacementGeometry.ComputeRectInclusive(ui.PlaceFirstCorner.Value, clampedWorldPos);
        int zMin = context.CurrentZ;
        int zMax = context.CurrentZ;

        context.Runtime.QueueConstructionOrder(
            rect,
            zMin,
            zMax,
            FortressPlacementRequestFactory.ToRuntimeConstructionShape(ui.SelectedConstructionShape),
            ui.ConstructionPreferredMaterialId,
            ui.ConstructionSelectedTags.ToArray(),
            priority: 50);

        Logger.Log($"[BUILD.UI] First=({ui.PlaceFirstCorner.Value.X},{ui.PlaceFirstCorner.Value.Y}) Second=({clampedWorldPos.X},{clampedWorldPos.Y}) Rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) Z={zMin}..{zMax}");
        Logger.Log($"[BUILD.UI] Enqueue construction shape={ui.SelectedConstructionShape} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) z={zMin}..{zMax} tags=[{string.Join('|', ui.ConstructionSelectedTags)}]");
        ui.AddToast($"[BUILD] Enqueued {ui.SelectedConstructionShape} {rect.Width}x{rect.Height} at z={context.CurrentZ}", context.UiTick + 150);
        ui.CancelPlacement();
        context.Redraw();
        return true;
    }
}
