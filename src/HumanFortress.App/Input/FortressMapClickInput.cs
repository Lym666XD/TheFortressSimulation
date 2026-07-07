using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static class FortressMapClickInput
{
    public static bool TryResolveWorldPosition(
        Point localMousePosition,
        Point cameraPosition,
        int zoomLevel,
        int fortressSize,
        Point? lastMousePosition,
        out Point worldPosition)
    {
        if (lastMousePosition.HasValue)
        {
            worldPosition = lastMousePosition.Value;
            return true;
        }

        int safeZoom = Math.Max(1, zoomLevel);
        int worldX = cameraPosition.X + (localMousePosition.X / safeZoom);
        int worldY = cameraPosition.Y + (localMousePosition.Y / safeZoom);
        int maxPosition = fortressSize * 32 - 1;

        if (worldX < 0 || worldY < 0 || worldX > maxPosition || worldY > maxPosition)
        {
            worldPosition = default;
            return false;
        }

        worldPosition = new Point(worldX, worldY);
        return true;
    }
}
