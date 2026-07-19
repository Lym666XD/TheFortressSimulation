using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressOverlayClickController
{
    private static bool TryPassThroughToMap(FortressOverlayClickContext context, Point local)
    {
        if (!context.MapSurfaceAvailable)
            return false;

        var mapLocal = new Point(local.X - context.MapSurfacePosition.X, local.Y - context.MapSurfacePosition.Y);
        if (mapLocal.X < 0 || mapLocal.X >= context.MapSurfaceWidth || mapLocal.Y < 0 || mapLocal.Y >= context.MapSurfaceHeight)
            return false;

        context.MapLeftClick(mapLocal);

        return true;
    }
}
