using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class PlacementPreviewSnapshotBuilder
{
    private static Rectangle CreateRectangle(Point first, Point second)
    {
        int x = Math.Min(first.X, second.X);
        int y = Math.Min(first.Y, second.Y);
        int width = Math.Abs(first.X - second.X) + 1;
        int height = Math.Abs(first.Y - second.Y) + 1;
        return new Rectangle(x, y, width, height);
    }

    private static SimulationPlacementPreviewData CreateEmpty(Rectangle rect)
    {
        return new SimulationPlacementPreviewData(
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height,
            0,
            rect.Width * rect.Height,
            Array.Empty<PlacementPreviewCellView>());
    }
}
