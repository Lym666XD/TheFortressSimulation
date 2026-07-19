using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.Stockpile;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadBuilder
{
    private static WorldSaveStockpileZonePayloadData ToPayloadStockpileZone(StockpileZone zone)
    {
        return new WorldSaveStockpileZonePayloadData(
            zone.ZoneId,
            zone.Name,
            ToPayloadChunkKey(zone.HomeChunk),
            new WorldSaveStockpileFilterPayloadData(
                (int)zone.Filter.Mode,
                ToSortedArray(zone.Filter.Tags),
                ToSortedArray(zone.Filter.ItemIds),
                ToSortedArray(zone.Filter.Materials)),
            zone.Priority,
            zone.TargetStacks,
            zone.HysteresisLow,
            zone.HysteresisHigh,
            zone.Generation,
            zone.CreatedTick,
            zone.GetMemberChunksSnapshot()
                .Select(ToPayloadChunkKey)
                .ToArray());
    }
}
