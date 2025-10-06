using SadRogue.Primitives;

namespace HumanFortress.App.UI.Selection;

/// <summary>
/// Converts between screen-space cells and world-space cells for the map surface,
/// respecting the map surface position on screen, camera world top-left, zoom and bounds.
/// </summary>
public sealed class WorldCoordinateMapper : IWorldCoordinateMapper
{
    private Point _mapScreenTopLeft;
    private Point _cameraWorldTopLeft;
    private int _zoom;
    private int _worldSizeTiles;
    private int _mapW;
    private int _mapH;

    public void UpdateParameters(Point mapScreenTopLeft, Point cameraWorldTopLeft, int zoomLevel, int worldSizeTiles, int mapSurfaceWidth, int mapSurfaceHeight)
    {
        _mapScreenTopLeft = mapScreenTopLeft;
        _cameraWorldTopLeft = cameraWorldTopLeft;
        _zoom = Math.Max(1, zoomLevel);
        _worldSizeTiles = Math.Max(1, worldSizeTiles);
        _mapW = Math.Max(1, mapSurfaceWidth);
        _mapH = Math.Max(1, mapSurfaceHeight);
    }

    public bool TryScreenToWorld(Point screenCell, out Point worldCell)
    {
        // Convert to local map cell
        int localX = screenCell.X - _mapScreenTopLeft.X;
        int localY = screenCell.Y - _mapScreenTopLeft.Y;
        if (localX < 0 || localY < 0 || localX >= _mapW || localY >= _mapH)
        {
            worldCell = default;
            return false;
        }

        // Apply zoom and camera offset
        int wx = _cameraWorldTopLeft.X + (localX / _zoom);
        int wy = _cameraWorldTopLeft.Y + (localY / _zoom);

        // Clamp to world bounds [0, worldSize-1]
        if (wx < 0 || wy < 0 || wx >= _worldSizeTiles || wy >= _worldSizeTiles)
        {
            worldCell = default;
            return false;
        }
        worldCell = new Point(wx, wy);
        return true;
    }

    public Point WorldToScreen(Point worldCell)
    {
        // Reverse: (world - camera) * zoom + mapTopLeft
        int lx = (worldCell.X - _cameraWorldTopLeft.X) * _zoom;
        int ly = (worldCell.Y - _cameraWorldTopLeft.Y) * _zoom;
        return new Point(_mapScreenTopLeft.X + lx, _mapScreenTopLeft.Y + ly);
    }
}

