using SadConsole.Input;

namespace HumanFortress.App.States;

internal sealed partial class WorldMapState
{
    private readonly record struct WorldMapNavigationResult(bool Moved, bool CursorMoved);

    private WorldMapNavigationResult ApplyWorldMapNavigation(Keyboard keyboard, int worldWidth, int worldHeight, int moveSpeed)
    {
        bool moved = false;
        bool cursorMoved = false;
        bool control = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);

        if (!control)
        {
            cursorMoved = TryMoveCursorWithWasd(keyboard, worldWidth, worldHeight, moveSpeed);
        }

        if (control)
        {
            moved = TryMoveCamera(keyboard, worldWidth, worldHeight, moveSpeed);
        }
        else if (HasArrowMovement(keyboard))
        {
            cursorMoved |= TryMoveCursorWithArrows(keyboard, worldWidth, worldHeight, moveSpeed);
        }

        if (cursorMoved)
        {
            CenterCameraOnCursor(worldWidth, worldHeight);
            moved = true;
        }

        return new WorldMapNavigationResult(moved, cursorMoved);
    }

}
