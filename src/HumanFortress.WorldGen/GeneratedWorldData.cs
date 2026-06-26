using System;
using System.Linq;
using HumanFortress.Contracts.WorldGen;
using HumanFortress.Core.World;

namespace HumanFortress.WorldGen;

internal sealed class GeneratedWorldData : IGeneratedWorldData
{
    private readonly WorldTile[,]? _tiles;

    private GeneratedWorldData(bool success, WorldTile[,]? tiles, string errorMessage)
    {
        Success = success;
        _tiles = tiles;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }
    public string ErrorMessage { get; }

    public bool TryGetSize(out int width, out int height)
    {
        if (_tiles == null)
        {
            width = 0;
            height = 0;
            return false;
        }

        width = _tiles.GetLength(0);
        height = _tiles.GetLength(1);
        return width > 0 && height > 0;
    }

    public bool TryGetTileView(WorldMapTilePosition tilePosition, out WorldMapTileView view)
    {
        view = default;
        if (!TryGetTile(tilePosition, out var tile))
            return false;

        view = new WorldMapTileView(
            new WorldMapTilePosition(tilePosition.X, tilePosition.Y),
            tile.BiomeId,
            GetBiomeName(tile.BiomeId),
            GetBiomeDisplayKind(tile.BiomeId),
            tile.Elevation,
            tile.Temperature,
            tile.Rainfall,
            tile.Drainage,
            tile.IsEmbarkable,
            tile.GetEmbarkabilityFailures());
        return true;
    }

    public bool TryGetTileSnapshot(WorldMapTilePosition tilePosition, out WorldTileSnapshot snapshot)
    {
        snapshot = default;
        if (!TryGetTile(tilePosition, out var tile))
            return false;

        snapshot = new WorldTileSnapshot(
            tile.BiomeId,
            GetBiomeName(tile.BiomeId),
            tile.Elevation,
            tile.Temperature,
            tile.Rainfall,
            tile.Drainage,
            tile.RiverClass,
            tile.HasAquifer,
            tile.StoneSet?.ToArray() ?? Array.Empty<ushort>(),
            tile.LandmarkIds?.ToArray() ?? Array.Empty<int>());
        return true;
    }

    internal static GeneratedWorldData FromWorldGenResult(WorldGenResult result)
    {
        return new GeneratedWorldData(
            result.Success,
            result.Tiles,
            result.ErrorMessage ?? string.Empty);
    }

    private bool TryGetTile(WorldMapTilePosition tilePosition, out WorldTile tile)
    {
        tile = default;
        if (_tiles == null)
            return false;

        int width = _tiles.GetLength(0);
        int height = _tiles.GetLength(1);
        if (tilePosition.X < 0 ||
            tilePosition.Y < 0 ||
            tilePosition.X >= width ||
            tilePosition.Y >= height)
        {
            return false;
        }

        tile = _tiles[tilePosition.X, tilePosition.Y];
        return true;
    }

    private static string GetBiomeName(ushort biomeId)
    {
        return ((BiomeType)biomeId).ToString();
    }

    private static WorldMapBiomeDisplayKind GetBiomeDisplayKind(ushort biomeId)
    {
        return (BiomeType)biomeId switch
        {
            BiomeType.Ocean => WorldMapBiomeDisplayKind.Ocean,
            BiomeType.Lake => WorldMapBiomeDisplayKind.Lake,
            BiomeType.River => WorldMapBiomeDisplayKind.River,
            BiomeType.Mountain => WorldMapBiomeDisplayKind.Mountain,
            BiomeType.Hills => WorldMapBiomeDisplayKind.Hills,
            BiomeType.Desert => WorldMapBiomeDisplayKind.Desert,
            BiomeType.Tundra => WorldMapBiomeDisplayKind.Tundra,
            BiomeType.Glacier => WorldMapBiomeDisplayKind.Glacier,
            BiomeType.TemperateForest => WorldMapBiomeDisplayKind.TemperateForest,
            BiomeType.TropicalForest => WorldMapBiomeDisplayKind.TropicalForest,
            BiomeType.Taiga => WorldMapBiomeDisplayKind.Taiga,
            BiomeType.TemperateGrassland => WorldMapBiomeDisplayKind.TemperateGrassland,
            BiomeType.Savanna => WorldMapBiomeDisplayKind.Savanna,
            BiomeType.Swamp => WorldMapBiomeDisplayKind.Swamp,
            _ => WorldMapBiomeDisplayKind.Unknown
        };
    }
}
