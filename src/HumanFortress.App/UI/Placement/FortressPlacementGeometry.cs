using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.UI.Placement;

internal static class FortressPlacementGeometry
{
    public static Rectangle ComputeRectInclusive(Point a, Point b)
    {
        int x = Math.Min(a.X, b.X);
        int y = Math.Min(a.Y, b.Y);
        int width = Math.Abs(a.X - b.X) + 1;
        int height = Math.Abs(a.Y - b.Y) + 1;
        return new Rectangle(x, y, width, height);
    }

    public static Point ClampToWorld(Point point, RuntimeWorldBounds worldBounds)
    {
        if (worldBounds.IsEmpty)
            return point;

        int x = Math.Clamp(point.X, worldBounds.MinX, worldBounds.MaxXExclusive - 1);
        int y = Math.Clamp(point.Y, worldBounds.MinY, worldBounds.MaxYExclusive - 1);
        return new Point(x, y);
    }
}
