using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal readonly record struct FortressMouseHoverControllerContext(
    FortressViewState View,
    MapScreenSurface? MapSurface,
    FortressViewportSnapshot Viewport,
    int FortressSize,
    ISelectionTool? SelectionTool,
    ulong UiTick);

internal readonly record struct FortressMouseHoverControllerResult(
    bool Changed,
    Point? LastMousePosition,
    Point CursorPosition);

internal static class FortressMouseHoverController
{
    public static FortressMouseHoverControllerResult ApplyOverlayHover(
        FortressMouseHoverControllerContext context,
        Point overlayLocal)
    {
        if (!context.View.TryToMapLocal(overlayLocal, out var mapLocal))
        {
            return new FortressMouseHoverControllerResult(
                Changed: false,
                context.Viewport.LastMousePosition,
                context.Viewport.CursorPosition);
        }

        return Apply(context, mapLocal, updateSelection: false, logMapEvent: false);
    }

    public static FortressMouseHoverControllerResult Apply(
        FortressMouseHoverControllerContext context,
        Point mapLocal,
        bool updateSelection,
        bool logMapEvent)
    {
        var mapSurface = context.MapSurface;
        if (mapSurface == null)
        {
            return new FortressMouseHoverControllerResult(
                Changed: false,
                context.Viewport.LastMousePosition,
                context.Viewport.CursorPosition);
        }

        var hover = FortressMouseHoverInput.Handle(
            mapLocal,
            mapSurface.Surface.Width,
            mapSurface.Surface.Height,
            context.Viewport.CameraPosition,
            context.Viewport.ZoomLevel,
            context.FortressSize,
            context.Viewport.CurrentZ,
            context.Viewport.LastMousePosition,
            context.Viewport.CursorPosition);

        if (hover.Changed)
        {
            if (updateSelection && context.SelectionTool != null && context.SelectionTool.IsActive)
                context.SelectionTool.Update(hover.CursorPosition);

            if (logMapEvent && context.UiTick % 10 == 0)
            {
                Logger.Log($"[MOUSE-EVT] Hover world=({hover.CursorPosition.X},{hover.CursorPosition.Y},{context.Viewport.CurrentZ}) local=({mapLocal.X},{mapLocal.Y}) camera=({context.Viewport.CameraPosition.X},{context.Viewport.CameraPosition.Y}) zoom={context.Viewport.ZoomLevel}");
            }
        }

        return new FortressMouseHoverControllerResult(
            hover.Changed,
            hover.LastMousePosition,
            hover.CursorPosition);
    }
}
