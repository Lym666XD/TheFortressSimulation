using HumanFortress.App.Diagnostics;
using HumanFortress.App.Session;
using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;

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
        var frameData = runtime.GetFrameRenderData(
            context.LoadedSession.HasFortressMap,
            context.FortressSize,
            context.Viewport.CameraPosition,
            context.Viewport.CursorPosition,
            context.Viewport.CurrentZ,
            context.Viewport.ZoomLevel,
            context.MapSurface.Surface.Width,
            context.MapSurface.Surface.Height,
            cursorGlyph,
            context.LoadedSession.NavigationOverlay?.SnapshotMode ?? SimulationNavigationOverlayMode.None,
            context.LoadedSession.NavigationOverlay?.SelectedTarget,
            context.TileInspection.WorldPosition,
            context.TileInspection.Z);

        var presentedMapViewport = context.MapViewportPresenter.Present(frameData.MapViewport);

        FortressMapRenderer.Render(
            context.MapSurface,
            presentedMapViewport,
            context.LoadedSession.NavigationOverlay,
            frameData.NavigationOverlay);

        FortressUiOverlayRenderer.Render(new FortressUiOverlayRenderContext(
            context.UiSurface,
            context.MapSurface,
            context.Ui,
            context.Runtime,
            context.Diagnostics,
            context.LoadedSession.UiServices,
            context.UiOverlayPresenter,
            presentedMapViewport,
            context.Viewport.CameraPosition,
            context.Viewport.CursorPosition,
            context.Viewport.LastMousePosition,
            context.Viewport.CurrentZ,
            context.Viewport.ZoomLevel,
            context.FortressSize,
            context.UiTick));

        context.TileInspection.RenderPopup(
            context.UiSurface,
            frameData.TileInspection);
    }
}
