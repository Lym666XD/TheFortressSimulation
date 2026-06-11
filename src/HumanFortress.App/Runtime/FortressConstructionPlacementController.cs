using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal readonly record struct FortressConstructionPlacementContext(
    UiStore Ui,
    FortressRuntimeAccess Runtime,
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

        var filter = FortressPlacementCommandFactory.CreateMaterialFilter(
            ui.SelectedConstructionShape,
            ui.ConstructionPreferredMaterialId,
            ui.ConstructionSelectedTags.ToArray());

        context.Runtime.EnqueueCurrentTickCommand(FortressPlacementCommandFactory.CreateConstructionOrder(
            rect,
            zMin,
            zMax,
            ui.SelectedConstructionShape,
            filter,
            priority: 50));

        Logger.Log($"[BUILD.UI] First=({ui.PlaceFirstCorner.Value.X},{ui.PlaceFirstCorner.Value.Y}) Second=({clampedWorldPos.X},{clampedWorldPos.Y}) Rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) Z={zMin}..{zMax}");
        Logger.Log($"[BUILD.UI] Enqueue construction shape={ui.SelectedConstructionShape} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) z={zMin}..{zMax} tags=[{string.Join('|', ui.ConstructionSelectedTags)}]");
        ui.AddToast($"[BUILD] Enqueued {ui.SelectedConstructionShape} {rect.Width}x{rect.Height} at z={context.CurrentZ}", context.UiTick + 150);
        ui.CancelPlacement();
        context.Redraw();
        return true;
    }
}
