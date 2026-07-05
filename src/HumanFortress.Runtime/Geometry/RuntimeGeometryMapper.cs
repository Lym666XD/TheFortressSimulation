using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Geometry;

internal static class RuntimeGeometryMapper
{
    internal static Point ToSadRoguePoint(this RuntimePoint point) => new(point.X, point.Y);

    internal static Point? ToSadRoguePoint(this RuntimePoint? point)
    {
        return point.HasValue ? point.Value.ToSadRoguePoint() : null;
    }

    internal static Rectangle ToSadRogueRectangle(this RuntimeRect rect) =>
        new(rect.X, rect.Y, rect.Width, rect.Height);

    internal static RuntimeRect ToRuntimeRect(this Rectangle rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
