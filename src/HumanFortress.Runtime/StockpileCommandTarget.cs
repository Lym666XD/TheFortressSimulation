using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime;

internal sealed partial class StockpileCommandTarget : IStockpileCommandTarget
{
    private readonly World _world;
    private readonly Action<string>? _log;

    internal StockpileCommandTarget(World world, Action<string>? log = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _log = log;
    }

    bool IStockpileCommandTarget.CreateStockpile(Rectangle worldRect, int z, string presetId, ulong currentTick)
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

}
