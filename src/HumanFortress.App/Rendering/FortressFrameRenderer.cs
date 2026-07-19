using HumanFortress.App.Diagnostics;
using HumanFortress.App.Session;
using HumanFortress.App.UI;
using HumanFortress.App.UI.Placement;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;

namespace HumanFortress.App.Rendering;

internal sealed record FortressFrameRenderContext(
    MapScreenSurface? MapSurface,
    UiOverlaySurface? UiSurface,
    UiStore Ui,
    FortressViewRuntimePorts Runtime,
    IFortressDiagnosticsAccess Diagnostics,
    FortressLoadedSessionSnapshot LoadedSession,
    FortressViewportSnapshot Viewport,
    FortressMapViewportPresenterCache MapViewportPresenter,
    FortressUiOverlayPresenterCache UiOverlayPresenter,
    int FortressSize,
    ulong UiTick,
    FortressTileInspectionController TileInspection);

internal static class FortressFrameRenderer
{
    public static void Render(FortressFrameRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.UiSurface == null || context.MapSurface == null)
            return;

        var runtime = context.Runtime.Read;
        int cursorGlyph = context.Ui.Context == UiContext.Global ? 'X' : '.';
        var requestedViewport = context.Viewport.CreateGeometry(new RuntimeRect(
            context.MapSurface.Position.X,
            context.MapSurface.Position.Y,
            context.MapSurface.Surface.Width,
            context.MapSurface.Surface.Height));
        context.Ui.PruneHighlights(context.UiTick);
        var placementPreviewMouse = FortressPlacementGeometry.ClampToWorld(
            context.Viewport.LastMousePosition ?? context.Viewport.CursorPosition,
            requestedViewport.WorldBounds);
        var placementPreviewRequests = FortressPlacementPreviewRequests.Build(
            context.Ui,
            requestedViewport,
            placementPreviewMouse,
            includeActivePlacement: context.Ui.Context == UiContext.PlacingTool);
        var appFrame = runtime.GetCommittedAppFrame(new SimulationAppFrameRequestData(
            IncludeMapViewport: context.LoadedSession.HasFortressMap,
            Viewport: requestedViewport,
            CursorPosition: ToRuntimePoint(context.Viewport.CursorPosition),
            CursorGlyph: cursorGlyph,
            NavigationMode: context.LoadedSession.NavigationOverlay?.SnapshotMode ?? SimulationNavigationOverlayMode.None,
            SelectedNavigationTarget: ToRuntimePoint(context.LoadedSession.NavigationOverlay?.SelectedTarget),
            TileInspectionWorldPosition: ToRuntimePoint(context.TileInspection.WorldPosition),
            TileInspectionZ: context.TileInspection.Z,
            ShowZoneOverlay: context.Ui.QuickMenu == QuickMenuKind.Zones,
            IncludeManagementDrawer: NeedsManagementDrawerData(context.Ui.OpenDrawer),
            IncludeWorkDrawer: context.Ui.OpenDrawer == DrawerId.Work,
            IncludeDebugMenu: context.Ui.DebugOpen,
            StockpileDetailZoneId: context.LoadedSession.UiServices?.StockpileUI?.EditingZoneId,
            ZoneDetailId: context.LoadedSession.UiServices?.ZonesUI?.DetailPopupZoneId,
            PlacementPreviewRequests: placementPreviewRequests,
            NavigationPathRequest: context.LoadedSession.NavigationOverlay?.PendingPathRequest));
        if (!appFrame.IsAvailable)
        {
            context.MapSurface.Clear();
            FortressUiOverlayRenderer.Clear(context.UiSurface);
            context.MapViewportPresenter.Reset();
            context.UiOverlayPresenter.Reset();
            return;
        }

        var frameData = appFrame.FrameRender;
        if (context.LoadedSession.NavigationOverlay?.TryApplyCommittedPath(
                appFrame.NavigationPath) == true)
        {
            var path = appFrame.NavigationPath.Path;
            context.Ui.AddToast(
                $"Path: {path.Kind} len={path.Length} cost={path.TotalCost / 10.0:F1}",
                context.UiTick + 180);
        }

        var presentedMapViewport = context.MapViewportPresenter.Present(frameData.MapViewport);

        FortressMapRenderer.Render(
            context.MapSurface,
            presentedMapViewport,
            context.LoadedSession.NavigationOverlay,
            frameData.NavigationOverlay);

        FortressUiOverlayRenderer.Render(
            new FortressUiOverlayRenderContext(
                context.UiSurface,
                context.MapSurface,
                context.Ui,
                context.Runtime,
                context.Diagnostics,
                context.LoadedSession.UiServices,
                context.UiOverlayPresenter,
                presentedMapViewport,
                presentedMapViewport.Viewport,
                new SadRogue.Primitives.Point(
                    presentedMapViewport.Viewport.CameraWorldOrigin.X,
                    presentedMapViewport.Viewport.CameraWorldOrigin.Y),
                context.Viewport.CursorPosition,
                context.Viewport.LastMousePosition,
                presentedMapViewport.Viewport.CurrentZ,
                presentedMapViewport.Viewport.ZoomLevel,
                context.FortressSize,
                context.UiTick),
            appFrame.UiOverlay,
            appFrame.PlacementPreviews,
            appFrame.SimulationStatus);

        context.TileInspection.RenderPopup(
            context.UiSurface,
            frameData.TileInspection);
    }

    private static RuntimePoint ToRuntimePoint(SadRogue.Primitives.Point point) =>
        new(point.X, point.Y);

    private static RuntimePoint? ToRuntimePoint(SadRogue.Primitives.Point? point) =>
        point.HasValue ? ToRuntimePoint(point.Value) : null;

    private static bool NeedsManagementDrawerData(DrawerId drawer)
    {
        return drawer is DrawerId.Creature or DrawerId.Stock or DrawerId.PlacementManagement;
    }
}
