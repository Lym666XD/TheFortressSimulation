using HumanFortress.App.UI;
using HumanFortress.App.UI.Placement;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressPlacementOverlayRenderer
{
    private static void RenderAnchoredPlacementPreview(
        FortressUiOverlayRenderContext context,
        RuntimeViewportGeometry viewport,
        Point mouseWorld,
        SimulationBuildCatalogData buildCatalog,
        SimulationPlacementPreviewFrameData placementPreviews)
    {
        var ui = context.Ui;
        var mapSurface = context.MapSurface;
        var firstCorner = ui.PlaceFirstCorner!.Value;

        if (ui.PlaceMode == PlacementMode.StockpileSecondCorner)
        {
            context.UiServices?.StockpileUI.RenderPlacementPreview(mapSurface, firstCorner, mouseWorld, viewport, true);
        }
        else if (ui.PlaceMode == PlacementMode.HaulSecondCorner && context.UiServices?.OrdersUI != null)
        {
            var preview = FortressPlacementPreviewRequests.Find(
                placementPreviews,
                firstCorner,
                mouseWorld,
                context.CurrentZ,
                SimulationPlacementPreviewMode.GroundItems);
            context.UiServices.OrdersUI.RenderPlacementPreview(mapSurface, preview, viewport, true, showEligibleHint: false);
        }
        else if (ui.PlaceMode == PlacementMode.MiningSecondCorner && context.UiServices?.OrdersUI != null)
        {
            var previewMode = FortressPlacementPreviewRequests.ForMiningAction(
                ui.SelectedMiningAction);
            var preview = FortressPlacementPreviewRequests.Find(
                placementPreviews,
                firstCorner,
                mouseWorld,
                context.CurrentZ,
                previewMode);
            context.UiServices.OrdersUI.RenderPlacementPreview(
                mapSurface,
                preview,
                viewport,
                true,
                showEligibleHint: true);
        }
        else if (ui.PlaceMode == PlacementMode.ConstructionSecondCorner && context.UiServices?.OrdersUI != null)
        {
            RenderConstructionPlacementPreview(
                context,
                viewport,
                mouseWorld,
                firstCorner,
                placementPreviews);
        }
        else if (ui.PlaceMode == PlacementMode.BuildableConfirmAnchor && ui.SelectedBuildableConstructionId != null)
        {
            RenderWorkshopPlacementPreview(
                mapSurface,
                firstCorner,
                viewport,
                ui.SelectedBuildableConstructionId,
                buildCatalog);
        }
    }

    private static void RenderConstructionPlacementPreview(
        FortressUiOverlayRenderContext context,
        RuntimeViewportGeometry viewport,
        Point mouseWorld,
        Point firstCorner,
        SimulationPlacementPreviewFrameData placementPreviews)
    {
        var ui = context.Ui;
        var previewMode = FortressPlacementPreviewRequests.ForConstructionShape(
            ui.SelectedConstructionShape);
        var preview = FortressPlacementPreviewRequests.Find(
            placementPreviews,
            firstCorner,
            mouseWorld,
            context.CurrentZ,
            previewMode);
        context.UiServices!.OrdersUI.RenderPlacementPreview(context.MapSurface, preview, viewport, true, showEligibleHint: false);
        var dynRect = FortressPlacementGeometry.ComputeRectInclusive(firstCorner, mouseWorld);
        ui.AddHighlight($"construction:{ui.SelectedConstructionShape}", dynRect, context.CurrentZ, context.CurrentZ, context.UiTick + 2);
    }
}
