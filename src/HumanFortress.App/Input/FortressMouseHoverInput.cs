using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static class FortressMouseHoverInput
{
    public static FortressMouseHoverResult Handle(
        Point localMousePosition,
        RuntimeViewportGeometry viewport,
        Point currentCursorPosition)
    {
        if (!RuntimeViewportGeometryMath.TryLocalToWorld(
                viewport,
                new RuntimePoint(localMousePosition.X, localMousePosition.Y),
                out var worldPoint))
        {
            return new FortressMouseHoverResult(false, null, currentCursorPosition);
        }

        var worldPosition = new Point(worldPoint.X, worldPoint.Y);
        Logger.Log($"[MOUSE] Hover tile world=({worldPosition.X},{worldPosition.Y},{viewport.CurrentZ})");
        return new FortressMouseHoverResult(true, worldPosition, worldPosition);
    }
}

internal readonly record struct FortressMouseHoverResult(bool Changed, Point? LastMousePosition, Point CursorPosition);
