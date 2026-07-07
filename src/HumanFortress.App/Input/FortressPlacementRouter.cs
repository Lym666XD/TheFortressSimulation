using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal readonly record struct FortressPlacementRouterContext(
    UiStore Ui,
    FortressPlacementRuntimePorts Runtime,
    FortressUiServices? UiServices,
    ISelectionTool? SelectionTool,
    int FortressSize,
    int CurrentZ,
    ulong UiTick,
    Action Redraw);

internal static class FortressPlacementRouter
{
    public static bool TryHandleClick(FortressPlacementRouterContext context, Point worldPos)
    {
        var ui = context.Ui;
        if (ui.Context != UiContext.PlacingTool)
            return false;

        if (FortressPlacementClickInput.TryHandleFirstCorner(ui, worldPos, context.CurrentZ, context.FortressSize, context.UiTick, context.SelectionTool))
        {
            context.Redraw();
            return true;
        }

        var placementContext = CreatePlacementControllerContext(context);
        return ui.PlaceMode switch
        {
            PlacementMode.StockpileSecondCorner => FortressPlacementController.TryHandleStockpileSecondCornerClick(placementContext, worldPos),
            PlacementMode.HaulSecondCorner => FortressPlacementController.TryHandleHaulSecondCornerClick(placementContext, worldPos),
            PlacementMode.MiningSecondCorner => FortressMiningPlacementController.TryHandleSecondCornerClick(CreateMiningPlacementContext(context), worldPos),
            PlacementMode.ConstructionSecondCorner when ui.PlaceFirstCorner.HasValue => FortressConstructionPlacementController.TryHandleSecondCornerClick(CreateConstructionPlacementContext(context), worldPos),
            PlacementMode.BuildableConfirmAnchor when ui.PlaceFirstCorner.HasValue => FortressPlacementController.TryHandleBuildableConfirmAnchorClick(placementContext),
            PlacementMode.ZoneSecondCorner when ui.PlaceFirstCorner.HasValue => FortressPlacementController.TryHandleZoneSecondCornerClick(placementContext, worldPos),
            PlacementMode.ZoneDelete => FortressPlacementController.TryHandleZoneDeleteClick(placementContext, worldPos),
            PlacementMode.StockpileDelete => FortressPlacementController.TryHandleStockpileDeleteClick(placementContext, worldPos),
            PlacementMode.StockpileCopy => FortressPlacementController.TryHandleStockpileCopyClick(placementContext, worldPos),
            _ => false
        };
    }

    public static void CreateStockpile(FortressPlacementRouterContext context, string presetId)
    {
        FortressPlacementController.CreateStockpile(CreatePlacementControllerContext(context), presetId);
    }

    private static FortressPlacementControllerContext CreatePlacementControllerContext(FortressPlacementRouterContext context)
    {
        return new FortressPlacementControllerContext(
            context.Ui,
            context.Runtime,
            context.UiServices?.StockpileUI,
            context.CurrentZ,
            context.UiTick,
            context.Redraw);
    }

    private static FortressMiningPlacementContext CreateMiningPlacementContext(FortressPlacementRouterContext context)
    {
        return new FortressMiningPlacementContext(
            context.Ui,
            context.Runtime,
            context.SelectionTool,
            context.FortressSize,
            context.CurrentZ,
            context.UiTick,
            context.Redraw);
    }

    private static FortressConstructionPlacementContext CreateConstructionPlacementContext(FortressPlacementRouterContext context)
    {
        return new FortressConstructionPlacementContext(
            context.Ui,
            context.Runtime,
            context.FortressSize,
            context.CurrentZ,
            context.UiTick,
            context.Redraw);
    }
}
