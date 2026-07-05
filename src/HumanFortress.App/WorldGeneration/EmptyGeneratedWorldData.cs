using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.App.WorldGeneration;

internal sealed class EmptyGeneratedWorldData : IGeneratedWorldData
{
    internal static EmptyGeneratedWorldData Instance { get; } = new();

    private EmptyGeneratedWorldData()
    {
    }

    public bool Success => false;
    public string ErrorMessage => string.Empty;

    public bool TryGetSize(out int width, out int height)
    {
        width = 0;
        height = 0;
        return false;
    }

    public bool TryGetTileView(WorldMapTilePosition tilePosition, out WorldMapTileView view)
    {
        view = default;
        return false;
    }

    public bool TryGetTileSnapshot(WorldMapTilePosition tilePosition, out WorldTileSnapshot snapshot)
    {
        snapshot = default;
        return false;
    }

    public bool TryFindNearestEmbarkableTile(WorldMapTilePosition origin, out WorldMapTilePosition tilePosition)
    {
        tilePosition = default;
        return false;
    }
}
