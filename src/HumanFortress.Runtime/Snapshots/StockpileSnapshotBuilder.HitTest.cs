using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class StockpileSnapshotBuilder
{
    internal static StockpileHitData FindAt(World? world, Point worldPosition, int z)
    {
        if (world == null)
            return StockpileHitData.Empty;

        int chunkX = worldPosition.X / Chunk.SIZE_XY;
        int chunkY = worldPosition.Y / Chunk.SIZE_XY;
        int localX = worldPosition.X % Chunk.SIZE_XY;
        int localY = worldPosition.Y % Chunk.SIZE_XY;
        if (localX < 0 || localY < 0 || localX >= Chunk.SIZE_XY || localY >= Chunk.SIZE_XY)
            return StockpileHitData.Empty;

        var chunkKey = new ChunkKey(chunkX, chunkY, z);
        var stockpileData = world.GetChunk(chunkKey)?.GetStockpileData();
        if (stockpileData == null)
            return StockpileHitData.Empty;

        int zoneId = stockpileData.GetZoneAtCell(localY * Chunk.SIZE_XY + localX);
        if (zoneId <= 0 || world.Stockpiles.GetZone(zoneId) == null)
            return StockpileHitData.Empty;

        return new StockpileHitData(true, zoneId, worldPosition.ToSnapshotPoint());
    }
}
