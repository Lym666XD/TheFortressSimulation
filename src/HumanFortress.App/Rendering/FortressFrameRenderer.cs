using HumanFortress.App.Runtime;
using HumanFortress.App.UI;

namespace HumanFortress.App.Rendering;

internal sealed record FortressFrameRenderContext(
    MapScreenSurface? MapSurface,
    UiOverlaySurface? UiSurface,
    UiStore Ui,
    FortressRuntimeAccess Runtime,
    FortressLoadedSessionSnapshot LoadedSession,
    FortressViewportSnapshot Viewport,
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

        FortressMapRenderer.Render(
            context.MapSurface,
            context.LoadedSession.FortressMap,
            context.LoadedSession.World,
            context.FortressSize,
            context.Viewport.CameraPosition,
            context.Viewport.CursorPosition,
            context.Viewport.CurrentZ,
            context.Viewport.ZoomLevel,
            context.Ui.Context,
            context.LoadedSession.NavigationOverlay,
            context.Runtime.Geology);

        FortressUiOverlayRenderer.Render(new FortressUiOverlayRenderContext(
            context.UiSurface,
            context.MapSurface,
            context.Ui,
            context.Runtime,
            context.LoadedSession.World,
            context.LoadedSession.UiServices,
            context.LoadedSession.CurrentSnapshot,
            context.LoadedSession.OverlayFromSnapshot,
            context.Viewport.CameraPosition,
            context.Viewport.CursorPosition,
            context.Viewport.LastMousePosition,
            context.Viewport.CurrentZ,
            context.Viewport.ZoomLevel,
            context.FortressSize,
            context.UiTick));

        context.TileInspection.RenderPopup(
            context.UiSurface,
            context.LoadedSession.FortressMap,
            context.LoadedSession.World,
            context.Runtime.Geology);
    }
}
