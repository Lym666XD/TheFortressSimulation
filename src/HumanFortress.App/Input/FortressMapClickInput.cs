using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static class FortressMapClickInput
{
    public static bool TryResolveWorldPosition(
        Point localMousePosition,
        RuntimeViewportGeometry viewport,
        out Point worldPosition)
    {
        if (!RuntimeViewportGeometryMath.TryLocalToWorld(
                viewport,
                new RuntimePoint(localMousePosition.X, localMousePosition.Y),
                out var resolved))
        {
            worldPosition = default;
            return false;
        }

        worldPosition = new Point(resolved.X, resolved.Y);
        return true;
    }
}
