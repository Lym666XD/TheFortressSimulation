using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressPlacementOverlayRenderer
{
    private static void RenderFloatingBuildablePreview(
        FortressUiOverlayRenderContext context,
        Rectangle viewport,
        Point mouseWorld,
        SimulationBuildCatalogData buildCatalog)
    {
        var ui = context.Ui;
        if (ui.PlaceMode != PlacementMode.BuildableFirstAnchor || ui.SelectedBuildableConstructionId == null)
            return;

        RenderWorkshopPlacementPreview(
            context.MapSurface,
            mouseWorld,
            viewport,
            ui.SelectedBuildableConstructionId,
            buildCatalog);
    }

    private static void RenderWorkshopPlacementPreview(
        ScreenSurface mapSurface,
        Point anchor,
        Rectangle viewport,
        string constructionId,
        SimulationBuildCatalogData buildCatalog)
    {
        var construction = buildCatalog.Workshops?
            .FirstOrDefault(candidate => string.Equals(candidate.Id, constructionId, StringComparison.Ordinal)) ?? default;
        if (string.IsNullOrWhiteSpace(construction.Id))
            return;

        FortressMapOverlayGlyphRenderer.DrawWorkshopPlacementPreview(mapSurface, anchor, construction.FootprintW, construction.FootprintD, viewport);
    }
}
