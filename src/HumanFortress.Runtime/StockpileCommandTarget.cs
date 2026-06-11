using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime;

public sealed class StockpileCommandTarget : IStockpileCommandTarget
{
    private readonly World _world;
    private readonly Action<string>? _log;

    public StockpileCommandTarget(World world, Action<string>? log = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _log = log;
    }

    public bool CreateStockpile(Rectangle worldRect, int z, string presetId, ulong currentTick)
    {
        string normalizedPreset = string.IsNullOrWhiteSpace(presetId) ? "all" : presetId;
        var cellsByChunk = new Dictionary<ChunkKey, List<int>>();
        int skippedInvalid = 0;
        int skippedOverlap = 0;

        for (int wx = worldRect.X; wx < worldRect.X + worldRect.Width; wx++)
        {
            for (int wy = worldRect.Y; wy < worldRect.Y + worldRect.Height; wy++)
            {
                TryCollectCell(wx, wy, z, cellsByChunk, ref skippedInvalid, ref skippedOverlap);
            }
        }

        int totalCells = cellsByChunk.Values.Sum(static list => list.Count);
        if (totalCells == 0)
        {
            _log?.Invoke($"[STOCKPILE] Skipped empty stockpile command preset={normalizedPreset} rect=({worldRect.X},{worldRect.Y},{worldRect.Width}x{worldRect.Height}) z={z} invalid={skippedInvalid} overlap={skippedOverlap}");
            return false;
        }

        var homeChunk = GetHomeChunk(cellsByChunk.Keys);
        int zoneId = _world.Stockpiles.CreateZone(BuildZoneName(normalizedPreset), homeChunk, currentTick);

        foreach (var (chunkKey, cells) in cellsByChunk)
        {
            var chunk = _world.GetChunk(chunkKey);
            if (chunk == null)
                continue;

            chunk.EnsureStockpileData();
            var stockpileData = chunk.GetStockpileData();
            stockpileData?.CreateOrUpdateShard(zoneId, chunkKey);
            stockpileData?.AddCellsToZone(zoneId, cells);
        }

        _world.Stockpiles.GetZone(zoneId)?.UpdateMemberChunks(cellsByChunk.Keys);
        _log?.Invoke($"[STOCKPILE] Created zone {zoneId} preset={normalizedPreset} cells={totalCells} invalid={skippedInvalid} overlap={skippedOverlap}");
        return true;
    }

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

    private static ChunkKey GetHomeChunk(IEnumerable<ChunkKey> chunkKeys)
    {
        return chunkKeys.OrderBy(static key => key.Z)
            .ThenBy(static key => key.ChunkY)
            .ThenBy(static key => key.ChunkX)
            .First();
    }

    private string BuildZoneName(string presetId)
    {
        int number = _world.Stockpiles.GetAllZones().Count() + 1;
        return presetId.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? $"Stockpile {number}"
            : $"{ToTitle(presetId)} Stockpile {number}";
    }

    private static string ToTitle(string value)
    {
        return value.Length == 0
            ? "Stockpile"
            : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
