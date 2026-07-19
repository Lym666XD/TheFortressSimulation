using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadRestorer
{
    private static void ValidateStockpileZonePayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.StockpileZones == null
            || payload.Counts.StockpileZoneCount != payload.StockpileZones.Length)
        {
            return;
        }

        var seenZoneIds = new HashSet<int>();
        for (var i = 0; i < payload.StockpileZones.Length; i++)
        {
            var zone = payload.StockpileZones[i];
            var prefix = $"World stockpile zone payload[{i}]";

            if (zone.ZoneId <= 0)
            {
                issues.Add($"{prefix} has non-positive zone id {zone.ZoneId}.");
            }
            else if (!seenZoneIds.Add(zone.ZoneId))
            {
                issues.Add($"{prefix} duplicates zone id {zone.ZoneId}.");
            }

            if (string.IsNullOrWhiteSpace(zone.Name))
            {
                issues.Add($"{prefix} has a blank name.");
            }

            ValidateWorldChunkKey(zone.HomeChunk, $"{prefix} home chunk", payload, issues);
            ValidateStockpileFilterArrays(zone.Filter, prefix, issues);

            if (zone.MemberChunks == null)
            {
                issues.Add($"{prefix} member chunks are missing.");
            }
            else
            {
                var seenMemberChunks = new HashSet<ChunkKey>();
                for (var j = 0; j < zone.MemberChunks.Length; j++)
                {
                    var memberKey = zone.MemberChunks[j];
                    ValidateWorldChunkKey(memberKey, $"{prefix} member chunk[{j}]", payload, issues);
                    if (!seenMemberChunks.Add(new ChunkKey(memberKey.ChunkX, memberKey.ChunkY, memberKey.Z)))
                    {
                        issues.Add($"{prefix} member chunk[{j}] duplicates chunk {memberKey.ChunkX},{memberKey.ChunkY},{memberKey.Z}.");
                    }
                }
            }
        }
    }

    private static void ValidateStockpileFilterArrays(
        WorldSaveStockpileFilterPayloadData filter,
        string prefix,
        ICollection<string> issues)
    {
        ValidateStringArray(filter.Tags, $"{prefix} filter tags", issues);
        ValidateStringArray(filter.ItemIds, $"{prefix} filter item ids", issues);
        ValidateStringArray(filter.Materials, $"{prefix} filter materials", issues);
    }
}
