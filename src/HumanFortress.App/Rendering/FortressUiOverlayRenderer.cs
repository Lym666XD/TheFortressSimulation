using HumanFortress.App.UI;
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
        UiRenderer.DrawDrawer(uiSurface, ui, context.UiTick, context.StockpileManager, world, context.Runtime);
        WorkDrawerOverlay.DrawWorkSchedulerOverlay(uiSurface, ui, context.UiTick, world);
        UiRenderer.DrawQuickMenu(
            uiSurface,
            ui,
            context.UiTick,
            context.OrdersUI,
            context.ZonesUI,
            context.BuildUI,
            context.StockpileQuickUI,
            cameraOverride: context.CameraPosition,
            zOverride: context.CurrentZ,
            world: world);

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
                UiRenderer.DrawWorkshopsOverlay(mapSurface, world, context.CurrentZ, viewport);
        }

        if (world != null && context.StockpileUI != null)
        {
            context.StockpileUI.RenderOverlay(mapSurface, world, context.CurrentZ, viewport);
        }

        if (world != null && context.ZonesUI != null)
        {
            bool showZoneOverlay = ui.QuickMenu == QuickMenuKind.Zones;
            context.ZonesUI.RenderOverlay(mapSurface, world, context.CurrentZ, viewport, showZoneOverlay);

            if (ui.PlaceMode == PlacementMode.ZoneSecondCorner && ui.PlaceFirstCorner.HasValue)
            {
                var mouseWorld = context.LastMousePosition ?? context.CursorPosition;
                context.ZonesUI.RenderPlacementPreview(mapSurface, ui.PlaceFirstCorner.Value, mouseWorld, viewport, true);
            }
        }
    }

    private static void RenderToolUi(FortressUiOverlayRenderContext context)
    {
        var ui = context.Ui;
        var uiSurface = context.UiSurface;
        var stockpileUI = context.StockpileUI;
        var zonesUI = context.ZonesUI;

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
        var mouseWorld = ClampToWorld(context.LastMousePosition ?? context.CursorPosition, context.FortressSize);

        context.OrdersUI?.DrawPlacementMode(context.UiSurface, ui, mouseWorld);
        context.StockpileUI?.DrawPlacementMode(context.UiSurface, ui, mouseWorld);

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
            context.StockpileUI?.RenderPlacementPreview(mapSurface, firstCorner, mouseWorld, viewport, true);
        }
        else if (ui.PlaceMode == PlacementMode.HaulSecondCorner && context.OrdersUI != null)
        {
            context.OrdersUI.RenderPlacementPreview(mapSurface, firstCorner, mouseWorld, viewport, true, context.CurrentZ, context.World);
        }
        else if (ui.PlaceMode == PlacementMode.MiningSecondCorner && context.OrdersUI != null)
        {
            context.OrdersUI.RenderPlacementPreview(
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
        else if (ui.PlaceMode == PlacementMode.ConstructionSecondCorner && context.OrdersUI != null)
        {
            context.OrdersUI.RenderPlacementPreview(mapSurface, firstCorner, mouseWorld, viewport, true, context.CurrentZ, context.World);
            var dynRect = ComputeRectInclusive(firstCorner, mouseWorld);
            ui.AddHighlight($"construction:{ui.SelectedConstructionShape}", dynRect, context.CurrentZ, context.CurrentZ, context.UiTick + 2);
        }
        else if (ui.PlaceMode == PlacementMode.BuildableConfirmAnchor && ui.SelectedBuildableConstructionId != null)
        {
            RenderWorkshopPlacementPreview(mapSurface, firstCorner, viewport, context.World, ui.SelectedBuildableConstructionId);
        }
    }

    private static void RenderFloatingBuildablePreview(FortressUiOverlayRenderContext context, Rectangle viewport, Point mouseWorld)
    {
        var ui = context.Ui;
        if (ui.PlaceMode != PlacementMode.BuildableFirstAnchor || ui.SelectedBuildableConstructionId == null)
            return;

        RenderWorkshopPlacementPreview(context.MapSurface, mouseWorld, viewport, context.World, ui.SelectedBuildableConstructionId);
    }

    private static void RenderWorkshopPlacementPreview(
        ScreenSurface mapSurface,
        Point anchor,
        Rectangle viewport,
        HumanFortress.Simulation.World.World? world,
        string constructionId)
    {
        var def = ConstructionRegistry.Instance.GetConstruction(constructionId);
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

        if (ui.ConstructionMaterialDialogOpen && context.BuildUI != null)
        {
            context.BuildUI.DrawConstructionMaterialDialog(uiSurface, ui);
        }

        if (world != null)
        {
            UiRenderer.DrawWorkshopPanel(uiSurface, ui, world, context.UiTick);
        }

        UiRenderer.DrawToasts(uiSurface, ui, context.UiTick);
    }

    private static Point ClampToWorld(Point p, int fortressSize)
    {
        int max = fortressSize * 32 - 1;
        int cx = p.X < 0 ? 0 : (p.X > max ? max : p.X);
        int cy = p.Y < 0 ? 0 : (p.Y > max ? max : p.Y);
        return new Point(cx, cy);
    }

    private static Rectangle ComputeRectInclusive(Point a, Point b)
    {
        int x = Math.Min(a.X, b.X);
        int y = Math.Min(a.Y, b.Y);
        int w = Math.Abs(a.X - b.X) + 1;
        int h = Math.Abs(a.Y - b.Y) + 1;
        return new Rectangle(x, y, w, h);
    }
}
