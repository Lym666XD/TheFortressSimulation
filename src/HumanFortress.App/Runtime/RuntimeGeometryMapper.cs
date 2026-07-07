using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal static class RuntimeGeometryMapper
{
    internal static RuntimePoint ToRuntimePoint(this Point point) => new(point.X, point.Y);

    internal static RuntimePoint? ToRuntimePoint(this Point? point)
    {
        return point.HasValue ? point.Value.ToRuntimePoint() : null;
    }

    internal static RuntimeRect ToRuntimeRect(this Rectangle rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    internal static Rectangle ToSadRogueRectangle(this RuntimeRect rect) =>
        new(rect.X, rect.Y, rect.Width, rect.Height);
}
