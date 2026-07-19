using HumanFortress.App.Rendering;
using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal readonly record struct FortressMouseHoverControllerContext(
    FortressViewState View,
    MapScreenSurface? MapSurface,
    FortressViewportSnapshot Viewport,
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
            context.Viewport.CreateGeometry(new RuntimeRect(
                0,
                0,
                mapSurface.Surface.Width,
                mapSurface.Surface.Height)),
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
