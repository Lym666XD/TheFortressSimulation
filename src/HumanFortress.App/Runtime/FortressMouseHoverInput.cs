using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal static class FortressMouseHoverInput
{
    public static FortressMouseHoverResult Handle(
        Point localMousePosition,
        int mapWidth,
        int mapHeight,
        Point cameraPosition,
        int zoomLevel,
        int fortressSize,
        int currentZ,
        Point? currentLastMousePosition,
        Point currentCursorPosition)
    {
        if (localMousePosition.X < 0
            || localMousePosition.X >= mapWidth
            || localMousePosition.Y < 0
            || localMousePosition.Y >= mapHeight)
        {
            return new FortressMouseHoverResult(false, null, currentCursorPosition);
        }

        int worldX = cameraPosition.X + (localMousePosition.X / zoomLevel);
        int worldY = cameraPosition.Y + (localMousePosition.Y / zoomLevel);
        int maxPos = fortressSize * 32 - 1;

        if (worldX < 0 || worldX > maxPos || worldY < 0 || worldY > maxPos)
            return new FortressMouseHoverResult(false, currentLastMousePosition, currentCursorPosition);

        var worldPosition = new Point(worldX, worldY);
        Logger.Log($"[MOUSE] Hover tile world=({worldPosition.X},{worldPosition.Y},{currentZ})");
        return new FortressMouseHoverResult(true, worldPosition, worldPosition);
    }
}

internal readonly record struct FortressMouseHoverResult(bool Changed, Point? LastMousePosition, Point CursorPosition);
