using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class StockpileCommandTarget
{
    private void TryCollectCell(
        int worldX,
        int worldY,
        int z,
        Dictionary<ChunkKey, List<int>> cellsByChunk,
        ref int skippedInvalid,
        ref int skippedOverlap)
    {
        if (!_world.IsValidPosition(worldX, worldY, z))
        {
            skippedInvalid++;
            return;
        }

        int chunkX = worldX / Chunk.SIZE_XY;
        int chunkY = worldY / Chunk.SIZE_XY;
        int localX = worldX % Chunk.SIZE_XY;
        int localY = worldY % Chunk.SIZE_XY;
        var chunkKey = new ChunkKey(chunkX, chunkY, z);
        var chunk = _world.GetChunk(chunkKey);
        if (chunk == null)
        {
            skippedInvalid++;
            return;
        }

        var tile = chunk.GetTile(localX, localY);
        if (tile.Kind != TerrainKind.OpenWithFloor)
        {
            skippedInvalid++;
            return;
        }

        int cellIndex = Chunk.LocalIndex(localX, localY);
        var stockpileData = chunk.GetStockpileData();
        if (stockpileData != null && stockpileData.GetZoneAtCell(cellIndex) != 0)
        {
            skippedOverlap++;
            return;
        }

        if (!cellsByChunk.TryGetValue(chunkKey, out var cells))
        {
            cells = new List<int>();
            cellsByChunk.Add(chunkKey, cells);
        }

        cells.Add(cellIndex);
    }

    private static ChunkKey GetHomeChunk(IReadOnlyList<KeyValuePair<ChunkKey, List<int>>> cellsByChunkRows)
    {
        return cellsByChunkRows[0].Key;
    }

    private static IOrderedEnumerable<KeyValuePair<ChunkKey, List<int>>> OrderCellsByChunk(
        IEnumerable<KeyValuePair<ChunkKey, List<int>>> cellsByChunk)
    {
        return cellsByChunk
            .OrderBy(static entry => entry.Key.Z)
            .ThenBy(static entry => entry.Key.ChunkY)
            .ThenBy(static entry => entry.Key.ChunkX);
    }
}
