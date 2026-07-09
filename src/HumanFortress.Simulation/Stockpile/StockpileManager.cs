using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Global manager for stockpile zones.
/// Maintains zone definitions and coordinates with chunks.
/// </summary>
internal sealed class StockpileManager
{
    private readonly Dictionary<int, StockpileZone> _zones = new();
    private readonly object _lock = new();
    private int _nextZoneId = 1;

    /// <summary>
    /// Create a new stockpile zone.
    /// </summary>
    internal int CreateZone(string name, ChunkKey homeChunk, ulong currentTick)
    {
        lock (_lock)
        {
            var zoneId = _nextZoneId++;
            var zone = new StockpileZone(zoneId, name, homeChunk, currentTick);
            _zones[zoneId] = zone;
            return zoneId;
        }
    }

    /// <summary>
    /// Get a zone by ID (thread-safe).
    /// </summary>
    internal StockpileZone? GetZone(int zoneId)
    {
        lock (_lock)
        {
            return _zones.GetValueOrDefault(zoneId);
        }
    }

    /// <summary>
    /// Get all zones (thread-safe).
    /// </summary>
    internal IEnumerable<StockpileZone> GetAllZones()
    {
        lock (_lock)
        {
            return _zones.Values
                .OrderBy(static zone => zone.ZoneId)
                .ToList();
        }
    }

    /// <summary>
    /// Delete a zone.
    /// </summary>
    internal bool DeleteZone(int zoneId)
    {
        lock (_lock)
        {
            return _zones.Remove(zoneId);
        }
    }

    /// <summary>
    /// Update zone configuration.
    /// </summary>
    internal void UpdateZone(int zoneId, Action<StockpileZone> update)
    {
        lock (_lock)
        {
            if (_zones.TryGetValue(zoneId, out var zone))
            {
                update(zone);
                zone.IncrementGeneration();
            }
        }
    }

    internal IReadOnlyList<string> RestoreZonesSnapshot(IReadOnlyList<WorldSaveStockpileZonePayloadData>? zones)
    {
        var issues = new List<string>();
        if (zones == null)
        {
            issues.Add("World stockpile zone payload is missing.");
            return issues;
        }

        var seen = new HashSet<int>();
        for (var i = 0; i < zones.Count; i++)
        {
            ValidateZonePayload(zones[i], i, seen, issues);
        }

        if (issues.Count > 0)
            return issues;

        lock (_lock)
        {
            _zones.Clear();
            var maxZoneId = 0;
            foreach (var payload in zones.OrderBy(zone => zone.ZoneId))
            {
                var zone = new StockpileZone(
                    payload.ZoneId,
                    payload.Name,
                    ToChunkKey(payload.HomeChunk),
                    ToFilter(payload.Filter),
                    payload.Priority,
                    payload.TargetStacks,
                    payload.HysteresisLow,
                    payload.HysteresisHigh,
                    payload.Generation,
                    payload.CreatedTick,
                    payload.MemberChunks.Select(ToChunkKey));
                _zones[zone.ZoneId] = zone;
                maxZoneId = Math.Max(maxZoneId, zone.ZoneId);
            }

            _nextZoneId = maxZoneId + 1;
        }

        return Array.Empty<string>();
    }

    private static void ValidateZonePayload(
        WorldSaveStockpileZonePayloadData payload,
        int index,
        ISet<int> seen,
        ICollection<string> issues)
    {
        var prefix = $"World stockpile zone payload[{index}]";

        if (payload.ZoneId <= 0)
        {
            issues.Add($"{prefix} has non-positive zone id {payload.ZoneId}.");
        }
        else if (!seen.Add(payload.ZoneId))
        {
            issues.Add($"{prefix} duplicates zone id {payload.ZoneId}.");
        }

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            issues.Add($"{prefix} has a blank name.");
        }

        if (!Enum.IsDefined(typeof(FilterMode), payload.Filter.Mode))
        {
            issues.Add($"{prefix} has unsupported filter mode {payload.Filter.Mode}.");
        }

        if (payload.Priority < 0)
        {
            issues.Add($"{prefix} has negative priority {payload.Priority}.");
        }

        if (payload.TargetStacks < 0)
        {
            issues.Add($"{prefix} has negative target stack count {payload.TargetStacks}.");
        }

        if (payload.HysteresisLow < 0 || payload.HysteresisHigh < 0)
        {
            issues.Add($"{prefix} has negative hysteresis thresholds.");
        }

        if (payload.HysteresisLow > payload.HysteresisHigh)
        {
            issues.Add($"{prefix} hysteresis low exceeds high.");
        }

        if (payload.Generation == 0)
        {
            issues.Add($"{prefix} has zero generation.");
        }

        if (payload.MemberChunks == null)
        {
            issues.Add($"{prefix} member chunks are missing.");
        }
    }

    private static StockpileFilter ToFilter(WorldSaveStockpileFilterPayloadData payload)
    {
        return new StockpileFilter
        {
            Mode = (FilterMode)payload.Mode,
            Tags = ToStringSet(payload.Tags),
            ItemIds = ToStringSet(payload.ItemIds),
            Materials = ToStringSet(payload.Materials)
        };
    }

    private static System.Collections.Immutable.ImmutableHashSet<string> ToStringSet(IEnumerable<string>? values)
    {
        return values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToImmutableHashSet(StringComparer.Ordinal)
            ?? System.Collections.Immutable.ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
    }

    private static ChunkKey ToChunkKey(WorldSaveChunkKeyData key)
    {
        return new ChunkKey(key.ChunkX, key.ChunkY, key.Z);
    }
}
