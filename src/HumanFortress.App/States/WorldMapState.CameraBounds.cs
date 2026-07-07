namespace HumanFortress.App.States;

internal sealed partial class WorldMapState
{
    private static int MaxCameraX(int worldWidth)
    {
        return Math.Max(0, worldWidth - MAP_WIDTH);
    }

    private static int MaxCameraY(int worldHeight)
    {
        return Math.Max(0, worldHeight - MAP_HEIGHT);
    }
}
