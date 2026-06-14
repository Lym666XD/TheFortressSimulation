using HumanFortress.App.UI;
using HumanFortress.App.Runtime;
using HumanFortress.Core.Content.Registry;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static class FortressUiOverlayRenderer
{
    public static void Render(FortressUiOverlayRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var uiSurface = context.UiSurface;
        var mapSurface = context.MapSurface;
        var ui = context.Ui;
        var world = context.World;

        uiSurface.Clear();
        UiRenderer.DrawTopBar(uiSurface, context.Runtime.SimulationStatus);
        UiRenderer.DrawDockScreen(uiSurface, ui, context.UiTick);
        UiRenderer.DrawQuickIconsScreen(uiSurface, ui, context.UiTick);
        UiRenderer.DrawDrawer(uiSurface, ui, context.UiTick, context.UiServices?.StockpileManager, world, context.Runtime);
        WorkDrawerOverlay.DrawWorkSchedulerOverlay(uiSurface, ui, context.UiTick, world);
        UiRenderer.DrawQuickMenu(
            uiSurface,
            ui,
            context.UiTick,
            context.UiServices?.OrdersUI,
            context.UiServices?.ZonesUI,
            context.UiServices?.BuildUI,
            context.UiServices?.StockpileQuickUI,
            cameraOverride: context.CameraPosition,
            zOverride: context.CurrentZ,
            world: world,
            constructions: context.Runtime.Constructions);

        RenderMapOverlays(context);
        RenderToolUi(context);
        RenderDebugAndModals(context);
    }

    private static void RenderMapOverlays(FortressUiOverlayRenderContext context)
    {
        var ui = context.Ui;
        var world = context.World;
        var mapSurface = context.MapSurface;
        var viewport = new Rectangle(
            context.CameraPosition.X,
            context.CameraPosition.Y,
            mapSurface.Surface.Width,
            mapSurface.Surface.Height);

        if (world != null)
        {
            ui.PruneHighlights(context.UiTick);
            UiRenderer.DrawOrderHighlights(mapSurface, ui, context.CameraPosition, context.CurrentZ, context.UiTick, world);
        }

        UiRenderer.DrawMiningJobHighlights(mapSurface, context.Runtime.MiningJobs, context.CameraPosition, context.CurrentZ, context.UiTick);
        UiRenderer.DrawMiningCompletedHighlights(mapSurface, context.Runtime.MiningJobs, context.CameraPosition, context.CurrentZ, context.UiTick);

        if (world != null)
        {
            if (context.OverlayFromSnapshot && context.CurrentSnapshot != null)
                UiRenderer.DrawWorkshopsOverlayFromSnapshot(mapSurface, context.CurrentSnapshot, context.CurrentZ, viewport);
            else
                UiRenderer.DrawWorkshopsOverlay(mapSurface, world, context.CurrentZ, viewport, context.Runtime.Constructions);
        }

        if (world != null && context.UiServices?.StockpileUI != null)
        {
            context.UiServices.StockpileUI.RenderOverlay(mapSurface, world, context.CurrentZ, viewport);
        }

        if (world != null && context.UiServices?.ZonesUI != null)
        {
            bool showZoneOverlay = ui.QuickMenu == QuickMenuKind.Zones;
            context.UiServices.ZonesUI.RenderOverlay(mapSurface, world, context.CurrentZ, viewport, showZoneOverlay);

            if (ui.PlaceMode == PlacementMode.ZoneSecondCorner && ui.PlaceFirstCorner.HasValue)
            {
                var mouseWorld = context.LastMousePosition ?? context.CursorPosition;
                context.UiServices.ZonesUI.RenderPlacementPreview(mapSurface, ui.PlaceFirstCorner.Value, mouseWorld, viewport, true);
            }
        }
    }

    private static void RenderToolUi(FortressUiOverlayRenderContext context)
    {
        var ui = context.Ui;
        var uiSurface = context.UiSurface;
        var stockpileUI = context.UiServices?.StockpileUI;
        var zonesUI = context.UiServices?.ZonesUI;

        if (stockpileUI != null)
        {
            if (ui.Context == UiContext.PlacingTool)
            {
                RenderPlacementMode(context);
            }

            stockpileUI.DrawEditPopup(uiSurface);
        }

        zonesUI?.DrawPlacementMode(uiSurface, ui, context.LastMousePosition ?? context.CursorPosition);

        if (zonesUI?.IsDetailPopupOpen() == true && context.World != null)
        {
            zonesUI.DrawDetailPopup(uiSurface, context.World);
        }
    }

    private static void RenderPlacementMode(FortressUiOverlayRenderContext context)
    {
        var ui = context.Ui;
        var mapSurface = context.MapSurface;
        var viewport = new Rectangle(
            context.CameraPosition.X,
            context.CameraPosition.Y,
            mapSurface.Surface.Width,
            mapSurface.Surface.Height);
        var mouseWorld = FortressPlacementGeometry.ClampToWorld(context.LastMousePosition ?? context.CursorPosition, context.FortressSize);

        context.UiServices?.OrdersUI.DrawPlacementMode(context.UiSurface, ui, mouseWorld);
        context.UiServices?.StockpileUI.DrawPlacementMode(context.UiSurface, ui, mouseWorld);

        if (ui.PlaceFirstCorner.HasValue)
        {
            RenderAnchoredPlacementPreview(context, viewport, mouseWorld);
            return;
        }

        RenderFloatingBuildablePreview(context, viewport, mouseWorld);
    }

    private static void RenderAnchoredPlacementPreview(FortressUiOverlayRenderContext context, Rectangle viewport, Point mouseWorld)
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
            context.UiServices.OrdersUI.RenderPlacementPreview(mapSurface, firstCorner, mouseWorld, viewport, true, context.CurrentZ, context.World);
        }
        else if (ui.PlaceMode == PlacementMode.MiningSecondCorner && context.UiServices?.OrdersUI != null)
        {
            context.UiServices.OrdersUI.RenderPlacementPreview(
                mapSurface,
                firstCorner,
                mouseWorld,
                viewport,
                true,
                context.CurrentZ,
                context.World,
                ui.SelectedMiningAction,
                ui.ShowIneligibleHints);
        }
        else if (ui.PlaceMode == PlacementMode.ConstructionSecondCorner && context.UiServices?.OrdersUI != null)
        {
            context.UiServices.OrdersUI.RenderPlacementPreview(mapSurface, firstCorner, mouseWorld, viewport, true, context.CurrentZ, context.World);
            var dynRect = FortressPlacementGeometry.ComputeRectInclusive(firstCorner, mouseWorld);
            ui.AddHighlight($"construction:{ui.SelectedConstructionShape}", dynRect, context.CurrentZ, context.CurrentZ, context.UiTick + 2);
        }
        else if (ui.PlaceMode == PlacementMode.BuildableConfirmAnchor && ui.SelectedBuildableConstructionId != null)
        {
            RenderWorkshopPlacementPreview(mapSurface, firstCorner, viewport, context.World, ui.SelectedBuildableConstructionId, context.Runtime.Constructions);
        }
    }

    private static void RenderFloatingBuildablePreview(FortressUiOverlayRenderContext context, Rectangle viewport, Point mouseWorld)
    {
        var ui = context.Ui;
        if (ui.PlaceMode != PlacementMode.BuildableFirstAnchor || ui.SelectedBuildableConstructionId == null)
            return;

        RenderWorkshopPlacementPreview(context.MapSurface, mouseWorld, viewport, context.World, ui.SelectedBuildableConstructionId, context.Runtime.Constructions);
    }

    private static void RenderWorkshopPlacementPreview(
        ScreenSurface mapSurface,
        Point anchor,
        Rectangle viewport,
        HumanFortress.Simulation.World.World? world,
        string constructionId,
        IConstructionCatalog? constructions)
    {
        var def = constructions?.GetConstruction(constructionId);
        if (def == null)
            return;

        var footprint = def.PlaceableProfile.Footprint;
        UiRenderer.DrawWorkshopPlacementPreview(mapSurface, anchor, footprint, viewport, world);
    }

    private static void RenderDebugAndModals(FortressUiOverlayRenderContext context)
    {
        var uiSurface = context.UiSurface;
        var ui = context.Ui;
        var world = context.World;

        UiRenderer.DrawDebug(
            uiSurface,
            ui,
            context.CursorPosition,
            context.CurrentZ,
            context.ZoomLevel,
            context.CameraPosition,
            context.FortressSize);

        if (world != null)
            DebugPageOverlayRenderer.PostDrawItemsPage(uiSurface, ui, world);

        UiRenderer.DrawDebugUnits(uiSurface, ui, context.CameraPosition.X, context.CameraPosition.Y, context.CurrentZ);
        UiRenderer.DrawPause(uiSurface, ui);

        if (ui.ConstructionMaterialDialogOpen && context.UiServices?.BuildUI != null)
        {
            context.UiServices.BuildUI.DrawConstructionMaterialDialog(uiSurface, ui);
        }

        if (world != null)
        {
            UiRenderer.DrawWorkshopPanel(uiSurface, ui, world, context.UiTick, context.Runtime.Constructions);
        }

        UiRenderer.DrawToasts(uiSurface, ui, context.UiTick);
    }

}
