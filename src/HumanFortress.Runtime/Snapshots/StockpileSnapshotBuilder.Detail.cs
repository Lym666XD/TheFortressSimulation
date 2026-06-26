using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class StockpileSnapshotBuilder
{
    internal static SimulationStockpileDetailData BuildDetail(World? world, int zoneId)
    {
        if (world == null)
            return SimulationStockpileDetailData.Empty;

        var zone = world.Stockpiles.GetZone(zoneId);
        if (zone == null)
            return SimulationStockpileDetailData.Empty;

        var (usedCells, totalCells) = CountCells(world, zone);
        return new SimulationStockpileDetailData(
            true,
            zone.ZoneId,
            zone.Name,
            zone.Priority,
            GetPriorityName(zone.Priority),
            GetFilterSummary(zone.Filter),
            usedCells,
            totalCells);
    }

    private static (int UsedCells, int TotalCells) CountCells(World world, StockpileZone zone)
    {
        int totalCells = 0;
        int usedCells = 0;
        foreach (var chunkKey in zone.MemberChunks)
        {
            var stockpileData = world.GetChunk(chunkKey)?.GetStockpileData();
            var shard = stockpileData?.GetShard(zone.ZoneId);
            if (shard == null)
                continue;

            totalCells += shard.Capacity;
            usedCells += shard.UsedSlots;
        }

        return (usedCells, totalCells);
    }

    private static string GetPriorityName(int priority)
    {
        return priority switch
        {
            0 => "Low",
            1 => "Normal",
            2 => "High",
            3 => "Critical",
            _ => "Unknown"
        };
    }

    private static string GetFilterSummary(StockpileFilter filter)
    {
        if (filter.Tags.Count == 0 && filter.ItemIds.Count == 0 && filter.Materials.Count == 0)
            return "All Items";

        if (filter.Tags.Count > 0)
            return string.Join(", ", filter.Tags.Take(3));

        if (filter.ItemIds.Count > 0)
            return string.Join(", ", filter.ItemIds.Take(3));

        if (filter.Materials.Count > 0)
            return string.Join(", ", filter.Materials.Take(3));

        return "Custom";
    }
}
