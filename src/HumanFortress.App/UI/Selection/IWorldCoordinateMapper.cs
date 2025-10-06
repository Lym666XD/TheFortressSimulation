using SadRogue.Primitives;

namespace HumanFortress.App.UI.Selection;

public interface IWorldCoordinateMapper
{
    bool TryScreenToWorld(Point screenCell, out Point worldCell);
    Point WorldToScreen(Point worldCell);
    void UpdateParameters(Point mapScreenTopLeft, Point cameraWorldTopLeft, int zoomLevel, int worldSizeTiles, int mapSurfaceWidth, int mapSurfaceHeight);
}

