namespace HumanFortress.Contracts.WorldGen;

public interface IGeneratedWorldData
{
    bool Success { get; }
    string ErrorMessage { get; }
    bool TryGetSize(out int width, out int height);
    bool TryGetTileView(WorldMapTilePosition tilePosition, out WorldMapTileView view);
    bool TryGetTileSnapshot(WorldMapTilePosition tilePosition, out WorldTileSnapshot snapshot);
}
