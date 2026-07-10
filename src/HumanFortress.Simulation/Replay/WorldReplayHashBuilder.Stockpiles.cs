using HumanFortress.Core.Determinism;
using HumanFortress.Simulation.Stockpile;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Replay;

internal static partial class WorldReplayHashBuilder
{
    private static string BuildStockpileZonesHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.stockpile_zones.snapshot.v1");
            AddStockpileZonesHash(hash, world.Stockpiles.GetAllZones());
        });
    }

    private static void AddStockpileZonesHash(ReplayHashBuilder hash, IEnumerable<StockpileZone> zones)
    {
        var ordered = zones
            .OrderBy(zone => zone.ZoneId)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var zone in ordered)
        {
            hash.AddInt32(zone.ZoneId);
            hash.AddString(zone.Name);
            AddChunkKeyHash(hash, zone.HomeChunk);
            hash.AddInt32((int)zone.Filter.Mode);
            AddStringSetHash(hash, zone.Filter.Tags);
            AddStringSetHash(hash, zone.Filter.ItemIds);
            AddStringSetHash(hash, zone.Filter.Materials);
            hash.AddInt32(zone.Priority);
            hash.AddInt32(zone.TargetStacks);
            hash.AddInt32(zone.HysteresisLow);
            hash.AddInt32(zone.HysteresisHigh);
            hash.AddInt32((int)zone.Generation);
            hash.AddUInt64(zone.CreatedTick);
            var memberChunks = zone.GetMemberChunksSnapshot();
            hash.AddInt32(memberChunks.Count);
            foreach (var chunk in memberChunks)
            {
                AddChunkKeyHash(hash, chunk);
            }
        }
    }
}
