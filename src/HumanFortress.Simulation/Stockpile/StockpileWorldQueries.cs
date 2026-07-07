using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Stockpile;

internal readonly record struct StockpileCellLocation(
    ChunkKey ChunkKey,
    int CellIndex,
    int ZoneId);

internal static class StockpileWorldQueries
{
    internal static bool TryGetStockpileCell(
        SimulationWorld world,
        int worldX,
        int worldY,
        int z,
        out StockpileCellLocation location)
    {
        ArgumentNullException.ThrowIfNull(world);
        location = default;

        if (worldX < 0 || worldY < 0 || z < 0)
            return false;

        int chunkX = worldX / Chunk.SIZE_XY;
        int chunkY = worldY / Chunk.SIZE_XY;
        int localX = worldX % Chunk.SIZE_XY;
        int localY = worldY % Chunk.SIZE_XY;
        var chunkKey = new ChunkKey(chunkX, chunkY, z);
        var chunk = world.GetChunk(chunkKey);
        var stockpileData = chunk?.GetStockpileData();
        if (stockpileData == null)
            return false;

        int cellIndex = Chunk.LocalIndex(localX, localY);
        int zoneId = stockpileData.GetZoneAtCell(cellIndex);
        if (zoneId <= 0 || world.Stockpiles.GetZone(zoneId) == null)
            return false;

        location = new StockpileCellLocation(chunkKey, cellIndex, zoneId);
        return true;
    }

    internal static bool IsItemInStockpile(SimulationWorld world, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return TryGetStockpileCell(world, item.Position.X, item.Position.Y, item.Z, out _);
    }

    internal static bool TryFindDestination(
        SimulationWorld world,
        ItemInstance item,
        IReadOnlyList<StockpileZone> zones,
        out Point destination,
        out int destinationZ,
        IReadOnlyDictionary<(ChunkKey ChunkKey, int ZoneId), int>? reservedByShard = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(zones);

        destination = default;
        destinationZ = item.Z;

        var itemChunkKey = new ChunkKey(
            item.Position.X / Chunk.SIZE_XY,
            item.Position.Y / Chunk.SIZE_XY,
            item.Z);
        var itemDefinition = world.Items.GetDefinition(item.DefinitionId);
        var filterItem = StockpileItemProjection.FromItem(item, itemDefinition);

        StockpileDestinationCandidate? best = null;
        foreach (var zone in zones.OrderBy(static zone => zone.ZoneId))
        {
            if (!zone.Filter.Accepts(filterItem))
                continue;

            foreach (var chunkKey in zone.MemberChunks
                .OrderBy(static key => key.Z)
                .ThenBy(static key => key.ChunkY)
                .ThenBy(static key => key.ChunkX))
            {
                var chunk = world.GetChunk(chunkKey);
                var stockpileData = chunk?.GetStockpileData();
                var shard = stockpileData?.GetShard(zone.ZoneId);
                if (shard == null)
                    continue;

                int pendingReservations = reservedByShard != null
                    && reservedByShard.TryGetValue((chunkKey, zone.ZoneId), out int reserved)
                        ? reserved
                        : 0;

                if (shard.GetAvailableCapacity() - pendingReservations <= 0)
                    continue;

                for (int cellIndex = 0; cellIndex < shard.MemberCells.Length; cellIndex++)
                {
                    if (!shard.MemberCells[cellIndex])
                        continue;

                    int distance = GetChunkDistance(itemChunkKey, chunkKey);
                    var candidate = new StockpileDestinationCandidate(zone.ZoneId, chunkKey, cellIndex, distance);
                    if (best == null || candidate.CompareTo(best.Value) < 0)
                        best = candidate;

                    break;
                }
            }
        }

        if (best == null)
            return false;

        var selected = best.Value;
        var (localX, localY) = Chunk.IndexToLocal(selected.CellIndex);
        destination = new Point(
            selected.ChunkKey.ChunkX * Chunk.SIZE_XY + localX,
            selected.ChunkKey.ChunkY * Chunk.SIZE_XY + localY);
        destinationZ = selected.ChunkKey.Z;
        return true;
    }

    private static int GetChunkDistance(ChunkKey from, ChunkKey to)
    {
        return Math.Abs(from.ChunkX - to.ChunkX)
            + Math.Abs(from.ChunkY - to.ChunkY)
            + Math.Abs(from.Z - to.Z);
    }

    private readonly record struct StockpileDestinationCandidate(
        int ZoneId,
        ChunkKey ChunkKey,
        int CellIndex,
        int Distance) : IComparable<StockpileDestinationCandidate>
    {
        public int CompareTo(StockpileDestinationCandidate other)
        {
            int cmp = Distance.CompareTo(other.Distance);
            if (cmp != 0) return cmp;

            cmp = ZoneId.CompareTo(other.ZoneId);
            if (cmp != 0) return cmp;

            cmp = ChunkKey.Z.CompareTo(other.ChunkKey.Z);
            if (cmp != 0) return cmp;

            cmp = ChunkKey.ChunkY.CompareTo(other.ChunkKey.ChunkY);
            if (cmp != 0) return cmp;

            cmp = ChunkKey.ChunkX.CompareTo(other.ChunkKey.ChunkX);
            if (cmp != 0) return cmp;

            return CellIndex.CompareTo(other.CellIndex);
        }
    }
}
