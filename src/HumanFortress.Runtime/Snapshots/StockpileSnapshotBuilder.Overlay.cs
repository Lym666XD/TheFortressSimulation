using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class StockpileSnapshotBuilder
{
    internal static SimulationStockpileOverlayData BuildOverlay(World? world, int currentZ, Rectangle viewport)
    {
        if (world == null)
            return SimulationStockpileOverlayData.Empty;

        var cells = new List<StockpileOverlayCellView>();
        foreach (var zone in world.Stockpiles.GetAllZones().OrderBy(zone => zone.ZoneId))
        {
            foreach (var chunkKey in zone.MemberChunks)
            {
                if (chunkKey.Z != currentZ || !IntersectsViewport(chunkKey, viewport))
                    continue;

                var stockpileData = world.GetChunk(chunkKey)?.GetStockpileData();
                var shard = stockpileData?.GetShard(zone.ZoneId);
                if (shard == null)
                    continue;

                for (int cellIndex = 0; cellIndex < shard.MemberCells.Length; cellIndex++)
                {
                    if (!shard.MemberCells[cellIndex])
                        continue;

                    var (localX, localY) = Chunk.IndexToLocal(cellIndex);
                    int worldX = chunkKey.ChunkX * Chunk.SIZE_XY + localX;
                    int worldY = chunkKey.ChunkY * Chunk.SIZE_XY + localY;

                    if (!Contains(viewport, worldX, worldY))
                        continue;

                    cells.Add(new StockpileOverlayCellView(worldX, worldY));
                }
            }
        }

        return cells.Count == 0
            ? SimulationStockpileOverlayData.Empty
            : new SimulationStockpileOverlayData(cells);
    }
}
