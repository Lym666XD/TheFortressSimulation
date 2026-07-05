using SadRogue.Primitives;
using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.App.Session;

internal sealed partial class FortressSessionContext
{
    internal bool HasGeneratedWorld => CurrentWorld.Success;

    internal bool TryGetWorldSize(out int width, out int height)
    {
        return CurrentWorld.TryGetSize(out width, out height);
    }

    internal bool TryGetWorldTileView(Point tilePosition, out WorldMapTileView view)
    {
        return CurrentWorld.TryGetTileView(new WorldMapTilePosition(tilePosition.X, tilePosition.Y), out view);
    }

    internal bool TryGetWorldTileSnapshot(Point tilePosition, out WorldTileSnapshot snapshot)
    {
        return CurrentWorld.TryGetTileSnapshot(new WorldMapTilePosition(tilePosition.X, tilePosition.Y), out snapshot);
    }

    internal bool TryFindNearestEmbarkableTile(Point origin, out Point tile)
    {
        tile = default;
        if (!CurrentWorld.TryFindNearestEmbarkableTile(new WorldMapTilePosition(origin.X, origin.Y), out var tilePosition))
            return false;

        tile = new Point(tilePosition.X, tilePosition.Y);
        return true;
    }

    private Point ClampToWorld(Point tile)
    {
        if (!TryGetWorldSize(out int width, out int height))
            return tile;

        if (width <= 0 || height <= 0)
            return default;

        return new Point(
            Math.Clamp(tile.X, 0, width - 1),
            Math.Clamp(tile.Y, 0, height - 1));
    }
}
