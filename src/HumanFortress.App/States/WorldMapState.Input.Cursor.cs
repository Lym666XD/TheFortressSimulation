using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class WorldMapState
{
    private bool TryMoveCursorWithWasd(Keyboard keyboard, int worldWidth, int worldHeight, int moveSpeed)
    {
        if (keyboard.IsKeyPressed(Keys.W))
        {
            _cursorPos = new Point(_cursorPos.X, Math.Max(0, _cursorPos.Y - moveSpeed));
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.S))
        {
            _cursorPos = new Point(_cursorPos.X, Math.Min(worldHeight - 1, _cursorPos.Y + moveSpeed));
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.A))
        {
            _cursorPos = new Point(Math.Max(0, _cursorPos.X - moveSpeed), _cursorPos.Y);
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.D))
        {
            _cursorPos = new Point(Math.Min(worldWidth - 1, _cursorPos.X + moveSpeed), _cursorPos.Y);
            return true;
        }

        return false;
    }

    private bool TryMoveCursorWithArrows(Keyboard keyboard, int worldWidth, int worldHeight, int moveSpeed)
    {
        if (keyboard.IsKeyPressed(Keys.Up))
        {
            _cursorPos = new Point(_cursorPos.X, Math.Max(0, _cursorPos.Y - moveSpeed));
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.Down))
        {
            _cursorPos = new Point(_cursorPos.X, Math.Min(worldHeight - 1, _cursorPos.Y + moveSpeed));
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.Left))
        {
            _cursorPos = new Point(Math.Max(0, _cursorPos.X - moveSpeed), _cursorPos.Y);
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.Right))
        {
            _cursorPos = new Point(Math.Min(worldWidth - 1, _cursorPos.X + moveSpeed), _cursorPos.Y);
            return true;
        }

        return false;
    }

    private static bool HasArrowMovement(Keyboard keyboard)
    {
        return keyboard.IsKeyPressed(Keys.Up)
            || keyboard.IsKeyPressed(Keys.Down)
            || keyboard.IsKeyPressed(Keys.Left)
            || keyboard.IsKeyPressed(Keys.Right);
    }
}
