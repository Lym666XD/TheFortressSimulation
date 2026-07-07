using HumanFortress.App.UI.Placement;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressPlacementController
{
    public static bool TryHandleBuildableConfirmAnchorClick(FortressPlacementControllerContext context)
    {
        var ui = context.Ui;
        if (!ui.PlaceFirstCorner.HasValue)
            return false;

        var anchor = ui.PlaceFirstCorner.Value;
        if (ui.SelectedBuildableConstructionId != null)
        {
            context.Runtime.QueueBuildableConstructionOrder(
                ui.SelectedBuildableConstructionId,
                anchor,
                context.CurrentZ,
                priority: 50);
            Logger.Log($"[BUILD.UI] Enqueue workshop id={ui.SelectedBuildableConstructionId} pos=({anchor.X},{anchor.Y}) z={context.CurrentZ}");
            ui.CancelPlacement();
            context.Redraw();
        }

        return true;
    }

    public static bool TryHandleZoneSecondCornerClick(
        FortressPlacementControllerContext context,
        Point worldPos)
    {
        var ui = context.Ui;
        if (!ui.PlaceFirstCorner.HasValue)
            return false;

        var rect = FortressPlacementGeometry.ComputeRectInclusive(ui.PlaceFirstCorner.Value, worldPos);

        if (ui.SelectedZoneDefId != null && context.Runtime.GetWorldAvailabilityData().HasWorld)
        {
            context.Runtime.QueueCreateZone(
                ui.SelectedZoneDefId,
                rect,
                context.CurrentZ);
            ui.AddToast($"Created zone at ({rect.X},{rect.Y})", context.UiTick + 150);
        }

        ui.CancelPlacement();
        context.Redraw();
        return true;
    }

    public static bool TryHandleZoneDeleteClick(
        FortressPlacementControllerContext context,
        Point worldPos)
    {
        var ui = context.Ui;
        var zoneHit = context.Runtime.FindZoneAt(worldPos, context.CurrentZ);
        if (zoneHit.HasZone)
        {
            context.Runtime.QueueDeleteZone(zoneHit.ZoneId);
            ui.AddToast($"Deleted zone #{zoneHit.ZoneId}", context.UiTick + 150);
        }
        else
        {
            ui.AddToast("No zone at this location", context.UiTick + 100);
        }

        context.Redraw();
        return true;
    }
}
