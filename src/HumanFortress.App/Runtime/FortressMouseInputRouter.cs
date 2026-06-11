using HumanFortress.App.UI;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal delegate bool FortressMouseHoverApplier(Point mapLocal, bool updateSelection, bool logMapEvent);

internal readonly record struct FortressMouseInputRouterContext(
    MapScreenSurface? MapSurface,
    UiOverlaySurface? UiSurface,
    FortressLoadedSessionSnapshot LoadedSession,
    UiStore Ui,
    int CurrentZ,
    ulong UiTick,
    bool TileInspectionOpen,
    Action EnsureFocus,
    FortressMouseHoverApplier ApplyMouseHover,
    Action HideTilePanel,
    Action RedrawAfterInput,
    Action Redraw,
    Action<Point> MapLeftClick);

internal readonly record struct FortressMouseInputResult(bool Handled, bool ShouldCallBase)
{
    public static readonly FortressMouseInputResult Unhandled = new(false, false);
    public static readonly FortressMouseInputResult HandledResult = new(true, false);
    public static readonly FortressMouseInputResult ContinueWithBase = new(false, true);
}

internal static class FortressMouseInputRouter
{
    public static FortressMouseInputResult Process(FortressMouseInputRouterContext context, MouseScreenObjectState state)
    {
        ArgumentNullException.ThrowIfNull(context.Ui);
        ArgumentNullException.ThrowIfNull(context.EnsureFocus);
        ArgumentNullException.ThrowIfNull(context.ApplyMouseHover);
        ArgumentNullException.ThrowIfNull(context.HideTilePanel);
        ArgumentNullException.ThrowIfNull(context.RedrawAfterInput);
        ArgumentNullException.ThrowIfNull(context.Redraw);
        ArgumentNullException.ThrowIfNull(context.MapLeftClick);

        var mapSurface = context.MapSurface;
        if (mapSurface == null || !context.LoadedSession.HasFortressMap)
            return FortressMouseInputResult.Unhandled;

        if (context.Ui.ConstructionMaterialDialogOpen)
            return FortressMouseInputResult.HandledResult;

        context.EnsureFocus();

        var mousePos = new Point(
            state.SurfaceCellPosition.X - mapSurface.Position.X,
            state.SurfaceCellPosition.Y - mapSurface.Position.Y);

        if (context.ApplyMouseHover(mousePos, updateSelection: false, logMapEvent: false))
            context.RedrawAfterInput();

        if (TryHandleScreenMouseClick(context, state, mapSurface)
            || TryHandleMapMouseClick(context, state, mapSurface)
            || TryHandleRightMouseClick(context, state))
        {
            return FortressMouseInputResult.HandledResult;
        }

        return FortressMouseInputResult.ContinueWithBase;
    }

    private static bool TryHandleScreenMouseClick(FortressMouseInputRouterContext context, MouseScreenObjectState state, MapScreenSurface mapSurface)
    {
        if (!state.Mouse.LeftClicked)
            return false;

        int screenWidth = GameHost.Instance?.ScreenCellsX ?? context.UiSurface?.Surface.Width ?? mapSurface.Surface.Width;
        int screenHeight = GameHost.Instance?.ScreenCellsY ?? context.UiSurface?.Surface.Height ?? mapSurface.Surface.Height;
        if (FortressScreenMouseInput.TryHandleDockClick(state.SurfaceCellPosition, screenHeight, context.Ui, context.HideTilePanel)
            || FortressScreenMouseInput.TryHandleQuickIconClick(state.SurfaceCellPosition, screenWidth, screenHeight, context.Ui, context.HideTilePanel)
            || FortressScreenMouseInput.TryHandleQuickMenuClick(state.SurfaceCellPosition, screenWidth, screenHeight, context.Ui, context.CurrentZ, context.UiTick))
        {
            context.Redraw();
            return true;
        }

        return false;
    }

    private static bool TryHandleMapMouseClick(FortressMouseInputRouterContext context, MouseScreenObjectState state, MapScreenSurface mapSurface)
    {
        if (!state.Mouse.LeftClicked)
            return false;

        var cell = state.SurfaceCellPosition - mapSurface.Position;
        if (cell.X < 0 || cell.X >= mapSurface.Surface.Width || cell.Y < 0 || cell.Y >= mapSurface.Surface.Height)
            return false;

        if (context.Ui.Context == UiContext.Global && !context.Ui.WorkshopPanelOpen)
            context.MapLeftClick(cell);

        return true;
    }

    private static bool TryHandleRightMouseClick(FortressMouseInputRouterContext context, MouseScreenObjectState state)
    {
        if (!state.Mouse.RightClicked)
            return false;

        FortressRightClickCancelInput.Handle(
            state.SurfaceCellPosition,
            context.Ui,
            context.TileInspectionOpen,
            context.LoadedSession.UiServices?.ZonesUI,
            context.LoadedSession.UiServices?.StockpileUI,
            context.HideTilePanel);
        Logger.Log("[RIGHT-CLICK] Handled successfully, redrawing UI");
        context.Redraw();
        return true;
    }
}
