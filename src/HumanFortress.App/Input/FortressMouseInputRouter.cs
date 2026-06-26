using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressMouseInputRouter
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
        if (mapSurface == null || !context.HasFortressMap)
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
}
