using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal readonly record struct FortressOverlayClickContext(
    UiStore Ui,
    int SurfaceWidth,
    int SurfaceHeight,
    bool MapSurfaceAvailable,
    Point MapSurfacePosition,
    int MapSurfaceWidth,
    int MapSurfaceHeight,
    FortressLoadedSessionSnapshot LoadedSession,
    FortressViewportSnapshot Viewport,
    ulong UiTick,
    bool TilePanelOpen,
    ISelectionTool? SelectionTool,
    Action HideTilePanel,
    Action Redraw,
    Action<Point> MapLeftClick);

internal static class FortressOverlayClickController
{
    public static bool HandleLeftClick(FortressOverlayClickContext context, Point local)
    {
        ArgumentNullException.ThrowIfNull(context.Ui);
        ArgumentNullException.ThrowIfNull(context.HideTilePanel);
        ArgumentNullException.ThrowIfNull(context.Redraw);
        ArgumentNullException.ThrowIfNull(context.MapLeftClick);

        if (context.Ui.ConstructionMaterialDialogOpen)
            return true;

        if (context.Ui.DebugOpen)
        {
            if (FortressOverlayMouseInput.TryHandleDebugSpawnClick(local, context.SurfaceWidth, context.SurfaceHeight, context.Ui, context.Viewport.CursorPosition, context.Viewport.CurrentZ, context.UiTick))
            {
                context.Redraw();
                return true;
            }

            if (FortressOverlayMouseInput.IsInsideDebugWindow(local, context.SurfaceWidth, context.SurfaceHeight))
                return true;
        }

        if (FortressOverlayMouseInput.TryHandleDockClick(local, context.SurfaceHeight, context.Ui, context.HideTilePanel))
        {
            context.Redraw();
            return true;
        }

        if (FortressOverlayMouseInput.TryHandleQuickClick(local, context.SurfaceWidth, context.SurfaceHeight, context.Ui, context.HideTilePanel))
        {
            context.Redraw();
            return true;
        }

        return TryPassThroughToMap(context, local);
    }

    public static bool HandleRightClick(FortressOverlayClickContext context, Point local)
    {
        ArgumentNullException.ThrowIfNull(context.Ui);
        ArgumentNullException.ThrowIfNull(context.HideTilePanel);
        ArgumentNullException.ThrowIfNull(context.Redraw);

        if (context.Ui.ConstructionMaterialDialogOpen)
            return true;

        Logger.Log($"[RIGHT-CLICK-OVERLAY] Clicked at local=({local.X},{local.Y}), tilePanelOpen={context.TilePanelOpen}, QuickMenu={context.Ui.QuickMenu}, OrdersMenu={context.Ui.OrdersMenu}, ZoneMenu={context.Ui.ZoneMenu}");

        if (context.SelectionTool != null && context.SelectionTool.IsActive)
        {
            context.SelectionTool.Cancel();
            context.Ui.CancelPlacement();
            context.Redraw();
            return true;
        }

        FortressRightClickCancelInput.Handle(
            local,
            context.Ui,
            context.TilePanelOpen,
            context.LoadedSession.UiServices?.ZonesUI,
            context.LoadedSession.UiServices?.StockpileUI,
            context.HideTilePanel);
        context.Redraw();
        return true;
    }

    private static bool TryPassThroughToMap(FortressOverlayClickContext context, Point local)
    {
        if (!context.MapSurfaceAvailable)
            return false;

        var mapLocal = new Point(local.X - context.MapSurfacePosition.X, local.Y - context.MapSurfacePosition.Y);
        if (mapLocal.X < 0 || mapLocal.X >= context.MapSurfaceWidth || mapLocal.Y < 0 || mapLocal.Y >= context.MapSurfaceHeight)
            return false;

        if (context.Viewport.LastMousePosition.HasValue)
        {
            var worldPos = context.Viewport.LastMousePosition.Value;
            var fakeLocal = new Point(
                (worldPos.X - context.Viewport.CameraPosition.X) * context.Viewport.ZoomLevel,
                (worldPos.Y - context.Viewport.CameraPosition.Y) * context.Viewport.ZoomLevel);
            context.MapLeftClick(fakeLocal);
        }
        else
        {
            context.MapLeftClick(mapLocal);
        }

        return true;
    }
}
