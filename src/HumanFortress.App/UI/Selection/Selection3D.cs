using SadRogue.Primitives;

namespace HumanFortress.App.UI.Selection;

/// <summary>
/// Immutable 3D selection (XY rectangle + Z range). Inclusive bounds.
/// </summary>
public readonly record struct Selection3D(Rectangle XY, int ZMin, int ZMax)
{
    public bool IsEmpty => XY.Width <= 0 || XY.Height <= 0 || ZMin > ZMax;
    public int Width => XY.Width;
    public int Height => XY.Height;
    public int Depth => ZMax - ZMin + 1;

    public Selection3D WithZRange(int zMin, int zMax)
        => new Selection3D(XY, Math.Min(zMin, zMax), Math.Max(zMin, zMax));

    public static Selection3D FromCorners(Point a, Point b, int zMin, int zMax)
    {
        int x = Math.Min(a.X, b.X);
        int y = Math.Min(a.Y, b.Y);
        int w = Math.Abs(a.X - b.X) + 1;
        int h = Math.Abs(a.Y - b.Y) + 1;
        return new Selection3D(new Rectangle(x, y, w, h), Math.Min(zMin, zMax), Math.Max(zMin, zMax));
    }
}

