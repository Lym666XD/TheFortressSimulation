using HumanFortress.App.UI;
using SadConsole;
using SadConsole.Input;

namespace HumanFortress.App.Input;

internal static partial class FortressMouseInputRouter
{
    private static bool TryHandleScreenMouseClick(FortressMouseInputRouterContext context, MouseScreenObjectState state, MapScreenSurface mapSurface)
    {
        if (!state.Mouse.LeftClicked)
            return false;

        int screenWidth = GameHost.Instance?.ScreenCellsX ?? context.UiSurface?.Surface.Width ?? mapSurface.Surface.Width;
        int screenHeight = GameHost.Instance?.ScreenCellsY ?? context.UiSurface?.Surface.Height ?? mapSurface.Surface.Height;
        if (FortressScreenMouseInput.TryHandleDockClick(state.SurfaceCellPosition, screenWidth, screenHeight, context.Ui, context.HideTilePanel)
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
            context.UiServices?.ZonesUI,
            context.UiServices?.StockpileUI,
            context.HideTilePanel);
        Logger.Log("[RIGHT-CLICK] Handled successfully, redrawing UI");
        context.Redraw();
        return true;
    }
}
