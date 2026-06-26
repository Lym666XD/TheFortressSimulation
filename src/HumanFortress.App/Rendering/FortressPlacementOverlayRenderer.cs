using HumanFortress.App.UI;
using HumanFortress.App.UI.Placement;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;
using UiMiningAction = HumanFortress.App.UI.MiningAction;

namespace HumanFortress.App.Rendering;

internal static partial class FortressPlacementOverlayRenderer
{
    public static void Render(
        FortressUiOverlayRenderContext context,
        SimulationBuildCatalogData buildCatalog)
    {
        var ui = context.Ui;
        var mapSurface = context.MapSurface;
        var viewport = new Rectangle(
            context.CameraPosition.X,
            context.CameraPosition.Y,
            mapSurface.Surface.Width,
            mapSurface.Surface.Height);
        var mouseWorld = FortressPlacementGeometry.ClampToWorld(
            context.LastMousePosition ?? context.CursorPosition,
            context.FortressSize);

        context.UiServices?.OrdersUI.DrawPlacementMode(context.UiSurface, ui, mouseWorld);
        context.UiServices?.StockpileUI.DrawPlacementMode(context.UiSurface, ui, mouseWorld);

        if (ui.PlaceFirstCorner.HasValue)
        {
            RenderAnchoredPlacementPreview(context, viewport, mouseWorld, buildCatalog);
            return;
        }

        RenderFloatingBuildablePreview(context, viewport, mouseWorld, buildCatalog);
    }

    private static SimulationPlacementPreviewMode ToPlacementPreviewMode(UiMiningAction miningAction)
    {
        return miningAction switch
        {
            UiMiningAction.Dig => SimulationPlacementPreviewMode.MiningDig,
            UiMiningAction.DigRamp => SimulationPlacementPreviewMode.MiningRamp,
            UiMiningAction.DigChannel => SimulationPlacementPreviewMode.MiningChannel,
            UiMiningAction.DigStairwell => SimulationPlacementPreviewMode.MiningStairwell,
            _ => SimulationPlacementPreviewMode.MiningDig,
        };
    }

    private static SimulationPlacementPreviewMode ToPlacementPreviewMode(UiConstructionShape constructionShape)
    {
        return constructionShape switch
        {
            UiConstructionShape.Floor => SimulationPlacementPreviewMode.ConstructionFloor,
            UiConstructionShape.Ramp => SimulationPlacementPreviewMode.ConstructionRamp,
            _ => SimulationPlacementPreviewMode.ConstructionWall,
        };
    }
}
