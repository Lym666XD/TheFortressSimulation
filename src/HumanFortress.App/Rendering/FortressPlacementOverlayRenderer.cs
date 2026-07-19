using HumanFortress.App.UI;
using HumanFortress.App.UI.Placement;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressPlacementOverlayRenderer
{
    public static void Render(
        FortressUiOverlayRenderContext context,
        SimulationBuildCatalogData buildCatalog,
        SimulationPlacementPreviewFrameData placementPreviews)
    {
        var ui = context.Ui;
        var mapSurface = context.MapSurface;
        var viewport = context.ViewportGeometry;
        var mouseWorld = FortressPlacementGeometry.ClampToWorld(
            context.LastMousePosition ?? context.CursorPosition,
            viewport.WorldBounds);

        context.UiServices?.OrdersUI.DrawPlacementMode(context.UiSurface, ui, mouseWorld);
        context.UiServices?.StockpileUI.DrawPlacementMode(context.UiSurface, ui, mouseWorld);

        if (ui.PlaceFirstCorner.HasValue)
        {
            RenderAnchoredPlacementPreview(
                context,
                viewport,
                mouseWorld,
                buildCatalog,
                placementPreviews);
            return;
        }

        RenderFloatingBuildablePreview(context, viewport, mouseWorld, buildCatalog);
    }
}
