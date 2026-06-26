using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static class SnapshotPrimitiveMapper
{
    internal static SnapshotColor ToSnapshotColor(this Color color) => new(color.R, color.G, color.B);

    internal static SnapshotPoint ToSnapshotPoint(this Point point) => new(point.X, point.Y);
}
