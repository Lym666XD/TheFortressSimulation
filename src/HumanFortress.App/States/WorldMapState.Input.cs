using SadConsole.Input;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class WorldMapState
{
    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        if (!_session.TryGetWorldSize(out int worldWidth, out int worldHeight))
        {
            if (keyboard.IsKeyPressed(Keys.Escape))
                _navigator.ShowMainMenu();

            return true;
        }

        bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        int moveSpeed = shift ? 10 : 1;

        var navigation = ApplyWorldMapNavigation(keyboard, worldWidth, worldHeight, moveSpeed);
        bool mapNeedsRender = navigation.Moved || navigation.CursorMoved;

        if (keyboard.IsKeyPressed(Keys.E) &&
            _session.TryFindNearestEmbarkableTile(_cursorPos, out var embarkableTile))
        {
            _cursorPos = embarkableTile;
            CenterCameraOnCursor(worldWidth, worldHeight);
            mapNeedsRender = true;
        }

        if (mapNeedsRender)
        {
            RenderMap();
        }

        if (keyboard.IsKeyPressed(Keys.Enter))
        {
            if (_session.TryGetWorldTileView(_cursorPos, out var tile) && tile.IsEmbarkable)
            {
                _session.SelectEmbarkTile(new Point(_cursorPos.X, _cursorPos.Y));
                _navigator.ShowEmbarkPreparation();
            }
        }
        else if (keyboard.IsKeyPressed(Keys.Escape))
        {
            _navigator.ShowMainMenu();
        }

        return true;
    }
}
