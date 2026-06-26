using HumanFortress.App.Rendering;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressOverlayClickController
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

        if (FortressOverlayMouseInput.TryHandleDockClick(local, context.SurfaceWidth, context.SurfaceHeight, context.Ui, context.HideTilePanel))
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
}
