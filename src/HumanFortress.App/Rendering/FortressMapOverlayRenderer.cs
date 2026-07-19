using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static class FortressMapOverlayRenderer
{
    public static void Render(
        FortressUiOverlayRenderContext context,
        SimulationUiOverlayFrameData overlayData,
        SimulationPlacementPreviewFrameData placementPreviews,
        RuntimeViewportGeometry viewport)
    {
        var ui = context.Ui;
        var mapSurface = context.MapSurface;

        if (context.MapViewport.HasWorld)
        {
            FortressMapOverlayGlyphRenderer.DrawOrderHighlights(
                mapSurface,
                ui,
                viewport,
                context.CurrentZ,
                context.UiTick,
                (first, second, mode) => FortressPlacementPreviewRequests.Find(
                    placementPreviews,
                    first,
                    second,
                    context.CurrentZ,
                    mode));
        }

        var jobs = overlayData.Jobs;
        FortressMapOverlayGlyphRenderer.DrawMiningJobHighlights(mapSurface, jobs?.ActiveMiningTargets, viewport, context.UiTick);
        FortressMapOverlayGlyphRenderer.DrawMiningCompletedHighlights(mapSurface, jobs?.RecentMiningCompletions, viewport);

        FortressMapOverlayGlyphRenderer.DrawWorkshopsOverlay(mapSurface, overlayData.Workshops, context.CurrentZ, viewport);

        if (context.UiServices?.StockpileUI != null)
        {
            context.UiServices.StockpileUI.RenderOverlay(mapSurface, overlayData.StockpileOverlay, viewport);
        }

        if (context.UiServices?.ZonesUI != null)
        {
            context.UiServices.ZonesUI.RenderOverlay(mapSurface, overlayData.ZoneOverlay, viewport);

            if (ui.PlaceMode == PlacementMode.ZoneSecondCorner && ui.PlaceFirstCorner.HasValue)
            {
                var mouseWorld = context.LastMousePosition ?? context.CursorPosition;
                context.UiServices.ZonesUI.RenderPlacementPreview(mapSurface, ui.PlaceFirstCorner.Value, mouseWorld, viewport, true);
            }
        }
    }
}
