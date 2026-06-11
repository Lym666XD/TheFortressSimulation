using HumanFortress.App.UI;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal readonly record struct FortressPlacementControllerContext(
    UiStore Ui,
    FortressRuntimeAccess Runtime,
    HumanFortress.Simulation.World.World? World,
    StockpileUI? StockpileUi,
    int CurrentZ,
    ulong UiTick,
    Action Redraw);

internal static class FortressPlacementController
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

    public static bool TryHandleHaulSecondCornerClick(
        FortressPlacementControllerContext context,
        Point worldPos)
    {
        var ui = context.Ui;
        if (ui.PlaceFirstCorner.HasValue && worldPos != ui.PlaceFirstCorner.Value)
        {
            ui.PlaceSecondCorner = worldPos;

            var rect = FortressPlacementGeometry.ComputeRectInclusive(ui.PlaceFirstCorner.Value, ui.PlaceSecondCorner.Value);
            context.Runtime.EnqueueCurrentTickCommand(FortressPlacementCommandFactory.CreateHaulOrder(rect, context.CurrentZ, priority: 50));
            ui.AddToast("Haul order created", context.UiTick + 120);
            Logger.Log($"[UI] Select first=({ui.PlaceFirstCorner.Value.X},{ui.PlaceFirstCorner.Value.Y},{context.CurrentZ}) second=({worldPos.X},{worldPos.Y},{context.CurrentZ}) -> rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height})");
            ui.CancelPlacement();
            context.Redraw();
        }

        return true;
    }

    public static bool TryHandleBuildableConfirmAnchorClick(FortressPlacementControllerContext context)
    {
        var ui = context.Ui;
        if (!ui.PlaceFirstCorner.HasValue)
            return false;

        var anchor = ui.PlaceFirstCorner.Value;
        if (ui.SelectedBuildableConstructionId != null)
        {
            context.Runtime.EnqueueCurrentTickCommand(FortressPlacementCommandFactory.CreateBuildableConstructionOrder(
                ui.SelectedBuildableConstructionId,
                anchor,
                context.CurrentZ,
                priority: 50));
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

        if (ui.SelectedZoneDefId != null && context.World != null)
        {
            context.Runtime.EnqueueCurrentTickCommand(FortressPlacementCommandFactory.CreateZone(
                ui.SelectedZoneDefId,
                rect,
                context.CurrentZ));
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
        int zoneId = context.World?.Zones.GetZoneAtPosition(worldPos.X, worldPos.Y, context.CurrentZ) ?? 0;
        if (zoneId > 0)
        {
            context.Runtime.EnqueueCurrentTickCommand(FortressPlacementCommandFactory.DeleteZone(zoneId));
            ui.AddToast($"Deleted zone #{zoneId}", context.UiTick + 150);
        }
        else
        {
            ui.AddToast("No zone at this location", context.UiTick + 100);
        }

        context.Redraw();
        return true;
    }

    public static bool TryHandleStockpileCopyClick(
        FortressPlacementControllerContext context,
        Point worldPos)
    {
        var ui = context.Ui;
        if (context.StockpileUi != null && context.World != null && context.StockpileUi.HandleStockpileClick(worldPos, context.CurrentZ, context.World))
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
        if (context.World == null || !ui.PlaceFirstCorner.HasValue || !ui.PlaceSecondCorner.HasValue)
            return;

        var rect = FortressPlacementGeometry.ComputeRectInclusive(ui.PlaceFirstCorner.Value, ui.PlaceSecondCorner.Value);

        context.Runtime.EnqueueCurrentTickCommand(FortressPlacementCommandFactory.CreateStockpile(rect, context.CurrentZ, presetId));

        int selectedCells = rect.Width * rect.Height;
        ui.AddToast($"Stockpile order queued ({selectedCells} tiles)", context.UiTick + 150);
        Logger.Log($"[STOCKPILE.UI] Enqueue preset={presetId} rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) z={context.CurrentZ}");
    }
}
