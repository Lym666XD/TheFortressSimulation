using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed record FortressInitialViewport(Point CameraPosition, Point CursorPosition);

internal static class FortressViewportMath
{
    public static FortressInitialViewport CreateInitial(int fortressSize)
    {
        int centerPos = (fortressSize * 32) / 2;
        return new FortressInitialViewport(
            new Point(Math.Max(0, centerPos - 40), Math.Max(0, centerPos - 20)),
            new Point(centerPos, centerPos));
    }

    public static Point ClampCamera(
        Point cameraPosition,
        int fortressSize,
        int mapSurfaceWidth,
        int mapSurfaceHeight,
        int zoomLevel)
    {
        int viewWidth = Math.Max(1, mapSurfaceWidth / zoomLevel);
        int viewHeight = Math.Max(1, mapSurfaceHeight / zoomLevel);
        int worldSize = fortressSize * 32;
        int maxCameraX = Math.Max(0, worldSize - viewWidth);
        int maxCameraY = Math.Max(0, worldSize - viewHeight);

        return new Point(
            Math.Max(0, Math.Min(maxCameraX, cameraPosition.X)),
            Math.Max(0, Math.Min(maxCameraY, cameraPosition.Y)));
    }
}
