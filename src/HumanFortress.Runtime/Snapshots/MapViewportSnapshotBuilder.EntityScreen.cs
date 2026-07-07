namespace HumanFortress.Runtime.Snapshots;

internal static partial class MapViewportSnapshotBuilder
{
    private static bool IsOnScreen(int screenX, int screenY, int viewWidth, int viewHeight)
    {
        return screenX >= 0 && screenX < viewWidth && screenY >= 0 && screenY < viewHeight;
    }
}
