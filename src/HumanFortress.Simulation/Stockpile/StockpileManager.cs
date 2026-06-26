using System;
using System.Collections.Generic;
using System.Linq;
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
    public int CreateZone(string name, ChunkKey homeChunk, ulong currentTick)
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
    public StockpileZone? GetZone(int zoneId)
    {
        lock (_lock)
        {
            return _zones.GetValueOrDefault(zoneId);
        }
    }

    /// <summary>
    /// Get all zones (thread-safe).
    /// </summary>
    public IEnumerable<StockpileZone> GetAllZones()
    {
        lock (_lock)
        {
            return _zones.Values.ToList();
        }
    }

    /// <summary>
    /// Delete a zone.
    /// </summary>
    public bool DeleteZone(int zoneId)
    {
        lock (_lock)
        {
            return _zones.Remove(zoneId);
        }
    }

    /// <summary>
    /// Update zone configuration.
    /// </summary>
    public void UpdateZone(int zoneId, Action<StockpileZone> update)
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
}