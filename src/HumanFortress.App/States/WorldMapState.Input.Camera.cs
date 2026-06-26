using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class WorldMapState
{
    private bool TryMoveCamera(Keyboard keyboard, int worldWidth, int worldHeight, int moveSpeed)
    {
        if (keyboard.IsKeyPressed(Keys.W) || keyboard.IsKeyPressed(Keys.Up))
        {
            _cameraPos = new Point(_cameraPos.X, Math.Max(0, _cameraPos.Y - moveSpeed));
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.S) || keyboard.IsKeyPressed(Keys.Down))
        {
            _cameraPos = new Point(_cameraPos.X, Math.Min(MaxCameraY(worldHeight), _cameraPos.Y + moveSpeed));
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.A) || keyboard.IsKeyPressed(Keys.Left))
        {
            _cameraPos = new Point(Math.Max(0, _cameraPos.X - moveSpeed), _cameraPos.Y);
            return true;
        }
        if (keyboard.IsKeyPressed(Keys.D) || keyboard.IsKeyPressed(Keys.Right))
        {
            _cameraPos = new Point(Math.Min(MaxCameraX(worldWidth), _cameraPos.X + moveSpeed), _cameraPos.Y);
            return true;
        }

        return false;
    }

    private void CenterCameraOnCursor(int worldWidth, int worldHeight)
    {
        int newCameraX = _cursorPos.X - MAP_WIDTH / 2;
        int newCameraY = _cursorPos.Y - MAP_HEIGHT / 2;

        newCameraX = Math.Max(0, Math.Min(MaxCameraX(worldWidth), newCameraX));
        newCameraY = Math.Max(0, Math.Min(MaxCameraY(worldHeight), newCameraY));

        _cameraPos = new Point(newCameraX, newCameraY);
    }
}
