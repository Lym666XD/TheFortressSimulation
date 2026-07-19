using HumanFortress.App.UI.Selection;
using HumanFortress.Contracts.Runtime;
using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal sealed record FortressKeyboardNavigationResult(Point CameraPosition, int CurrentZ, bool Changed);

internal static class FortressKeyboardNavigationInput
{
    public static FortressKeyboardNavigationResult Handle(
        Keyboard keyboard,
        Point cameraPosition,
        int currentZ,
        RuntimeWorldBounds worldBounds,
        ISelectionTool? selectionTool)
    {
        ArgumentNullException.ThrowIfNull(keyboard);

        bool changed = false;
        int moveSpeed = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? 5 : 1;

        if (keyboard.IsKeyDown(Keys.W))
        {
            cameraPosition = new Point(cameraPosition.X, cameraPosition.Y - moveSpeed);
            changed = true;
        }
        else if (keyboard.IsKeyDown(Keys.S))
        {
            cameraPosition = new Point(cameraPosition.X, cameraPosition.Y + moveSpeed);
            changed = true;
        }
        else if (keyboard.IsKeyDown(Keys.A))
        {
            cameraPosition = new Point(cameraPosition.X - moveSpeed, cameraPosition.Y);
            changed = true;
        }
        else if (keyboard.IsKeyDown(Keys.D))
        {
            cameraPosition = new Point(cameraPosition.X + moveSpeed, cameraPosition.Y);
            changed = true;
        }
        else if (keyboard.IsKeyPressed(Keys.Q))
        {
            if (ShouldAdjustSelectionZ(keyboard, selectionTool))
                selectionTool!.AdjustZRange(-1);
            else
                currentZ = ClampZ(currentZ - 1, worldBounds);

            changed = true;
        }
        else if (keyboard.IsKeyPressed(Keys.E))
        {
            if (ShouldAdjustSelectionZ(keyboard, selectionTool))
                selectionTool!.AdjustZRange(+1);
            else
                currentZ = ClampZ(currentZ + 1, worldBounds);

            changed = true;
        }

        return new FortressKeyboardNavigationResult(cameraPosition, currentZ, changed);
    }

    internal static int ClampZ(int value, RuntimeWorldBounds worldBounds)
    {
        return worldBounds.IsEmpty
            ? 0
            : Math.Clamp(value, worldBounds.MinZ, worldBounds.MaxZExclusive - 1);
    }

    private static bool ShouldAdjustSelectionZ(Keyboard keyboard, ISelectionTool? selectionTool)
    {
        return selectionTool != null &&
               selectionTool.IsActive &&
               (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
    }
}
