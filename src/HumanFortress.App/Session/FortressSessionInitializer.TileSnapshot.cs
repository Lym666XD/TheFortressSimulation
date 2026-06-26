using HumanFortress.Contracts.WorldGen;
using SadRogue.Primitives;

namespace HumanFortress.App.Session;

internal sealed partial class FortressSessionInitializer
{
    private bool TryGetEmbarkTileSnapshot(Point embarkLocation, out WorldTileSnapshot worldTile)
    {
        var currentWorld = _session.CurrentWorld;
        if (!currentWorld.Success)
        {
            Logger.Log("[GenerateFortressMap] ERROR: CurrentWorld is null");
            worldTile = default;
            return false;
        }

        if (!currentWorld.TryGetSize(out int width, out int height))
        {
            Logger.Log("[GenerateFortressMap] WARNING: World tiles are null, using fallback");
            worldTile = default;
            return false;
        }

        Logger.Log($"[GenerateFortressMap] World tiles dimensions: {width}x{height}");

        if (embarkLocation.X < 0 ||
            embarkLocation.Y < 0 ||
            embarkLocation.X >= width ||
            embarkLocation.Y >= height)
        {
            Logger.Log($"[GenerateFortressMap] ERROR: EmbarkLocation {embarkLocation} out of bounds");
            worldTile = default;
            return false;
        }

        if (!currentWorld.TryGetTileSnapshot(new WorldMapTilePosition(embarkLocation.X, embarkLocation.Y), out worldTile))
        {
            Logger.Log($"[GenerateFortressMap] ERROR: EmbarkLocation {embarkLocation} unavailable");
            return false;
        }

        Logger.Log($"[GenerateFortressMap] Got world tile at {embarkLocation}");
        return true;
    }
}
