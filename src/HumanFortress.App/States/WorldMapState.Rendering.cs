using HumanFortress.App.Rendering;
using HumanFortress.App.Session;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class WorldMapState
{
    private void RenderMap()
    {
        if (!_session.TryGetWorldSize(out int worldWidth, out int worldHeight))
            return;

        _mapSurface.Clear();

        for (int sx = 0; sx < MAP_WIDTH; sx++)
        {
            for (int sy = 0; sy < MAP_HEIGHT; sy++)
            {
                int wx = _cameraPos.X + sx;
                int wy = _cameraPos.Y + sy;

                if (wx >= 0 && wx < worldWidth && wy >= 0 && wy < worldHeight)
                {
                    if (!_session.TryGetWorldTileView(new Point(wx, wy), out var tile))
                        continue;

                    var display = WorldMapTileDisplayMapper.FromTile(tile);
                    _mapSurface.SetGlyph(sx, sy, display.Glyph, display.Color);
                }
            }
        }

        int cursorScreenX = _cursorPos.X - _cameraPos.X;
        int cursorScreenY = _cursorPos.Y - _cameraPos.Y;
        if (cursorScreenX >= 0 && cursorScreenX < MAP_WIDTH &&
            cursorScreenY >= 0 && cursorScreenY < MAP_HEIGHT)
        {
            var existing = _mapSurface.GetGlyph(cursorScreenX, cursorScreenY);
            _mapSurface.SetGlyph(cursorScreenX, cursorScreenY, existing, Color.Yellow, Color.DarkGray);
        }

        UpdateInfoPanel();
    }

}
