using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Zones;

/// <summary>
/// Global manager for zone instances per ZONE_SPEC.md.
/// Maintains zone definitions and runtime instances.
/// Thread-safe for reads during Read phase.
/// </summary>
internal sealed class ZoneManager
{
    private readonly Dictionary<int, ZoneInstance> _zones = new();
    private readonly Dictionary<string, ZoneDefinition> _definitions = new();
    private readonly object _lock = new();
    private int _nextZoneId = 1;

    /// <summary>
    /// Register a zone definition from content data.
    /// </summary>
    public void RegisterDefinition(ZoneDefinitionData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        // Convert DTO to domain model
        var definition = new ZoneDefinition
        {
            Id = data.Id,
            Category = data.Category,
            DisplayName = data.DisplayName,
            Notes = data.Notes,
            UiHints = new ZoneUiHints
            {
                Glyph = data.UiHints.Glyph,
                Color = data.UiHints.Color,
                Keybind = data.UiHints.Keybind
            },
            DefaultPolicies = new ZonePolicies
            {
                NavCostMode = data.DefaultPolicies.NavCostMode,
                AllowsActions = new List<string>(data.DefaultPolicies.AllowsActions)
            }
        };

        lock (_lock)
        {
            _definitions[definition.Id] = definition;
        }
    }

    /// <summary>
    /// Get zone definition by ID.
    /// </summary>
    public ZoneDefinition? GetDefinition(string defId)
    {
        lock (_lock)
        {
            return _definitions.GetValueOrDefault(defId);
        }
    }

    /// <summary>
    /// Get all zone definitions.
    /// </summary>
    public IEnumerable<ZoneDefinition> GetAllDefinitions()
    {
        lock (_lock)
        {
            return _definitions.Values
                .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>
    /// Create a new zone instance.
    /// </summary>
    public int CreateZone(string defId, string name, ChunkKey homeChunk, ulong currentTick)
    {
        lock (_lock)
        {
            var zoneId = _nextZoneId++;
            var zone = new ZoneInstance(zoneId, defId, name, homeChunk, currentTick);
            _zones[zoneId] = zone;
            return zoneId;
        }
    }

    /// <summary>
    /// Get a zone by ID (thread-safe).
    /// </summary>
    public ZoneInstance? GetZone(int zoneId)
    {
        lock (_lock)
        {
            return _zones.GetValueOrDefault(zoneId);
        }
    }

    /// <summary>
    /// Get all zones (thread-safe).
    /// </summary>
    public IEnumerable<ZoneInstance> GetAllZones()
    {
        lock (_lock)
        {
            return _zones.Values
                .OrderBy(static zone => zone.ZoneId)
                .ToList();
        }
    }

    /// <summary>
    /// Get zones by category.
    /// </summary>
    public IEnumerable<ZoneInstance> GetZonesByCategory(string category)
    {
        lock (_lock)
        {
            return _zones.Values
                .Where(z => _definitions.TryGetValue(z.DefId, out var def) && def.Category == category)
                .OrderBy(static zone => zone.ZoneId)
                .ToList();
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
    public void UpdateZone(int zoneId, Action<ZoneInstance> update)
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

    /// <summary>
    /// Clear all zones (for testing/reset).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _zones.Clear();
            _nextZoneId = 1;
        }
    }
}
