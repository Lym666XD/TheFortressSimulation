using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

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

    public static Point ClampToWorld(Point point, int fortressSize)
    {
        int max = fortressSize * 32 - 1;
        int x = Math.Clamp(point.X, 0, max);
        int y = Math.Clamp(point.Y, 0, max);
        return new Point(x, y);
    }
}
