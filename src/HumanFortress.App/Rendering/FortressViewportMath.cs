using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed record FortressInitialViewport(Point CameraPosition, Point CursorPosition);

internal static class FortressViewportMath
{
    public static FortressInitialViewport CreateInitial(
        RuntimeWorldBounds worldBounds,
        RuntimeRect surface,
        int zoomLevel)
    {
        if (worldBounds.IsEmpty)
            return new FortressInitialViewport(new Point(0, 0), new Point(0, 0));

        int centerX = worldBounds.MinX + (worldBounds.Width / 2);
        int centerY = worldBounds.MinY + (worldBounds.Height / 2);
        int visibleWidth = Math.Max(1, (surface.Width + Math.Max(1, zoomLevel) - 1) / Math.Max(1, zoomLevel));
        int visibleHeight = Math.Max(1, (surface.Height + Math.Max(1, zoomLevel) - 1) / Math.Max(1, zoomLevel));
        var normalized = RuntimeViewportGeometryMath.Normalize(new RuntimeViewportGeometry(
            surface,
            new RuntimePoint(centerX - (visibleWidth / 2), centerY - (visibleHeight / 2)),
            zoomLevel,
            worldBounds.MinZ + (worldBounds.Depth / 2),
            worldBounds));
        return new FortressInitialViewport(
            new Point(normalized.CameraWorldOrigin.X, normalized.CameraWorldOrigin.Y),
            new Point(centerX, centerY));
    }

    public static Point ClampCamera(
        Point cameraPosition,
        RuntimeWorldBounds worldBounds,
        RuntimeRect surface,
        int zoomLevel,
        int currentZ)
    {
        var normalized = RuntimeViewportGeometryMath.Normalize(new RuntimeViewportGeometry(
            surface,
            new RuntimePoint(cameraPosition.X, cameraPosition.Y),
            zoomLevel,
            currentZ,
            worldBounds));
        return new Point(normalized.CameraWorldOrigin.X, normalized.CameraWorldOrigin.Y);
    }
}
