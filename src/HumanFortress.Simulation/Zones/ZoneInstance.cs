using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Zones;

/// <summary>
/// Runtime zone instance per ZONE_SPEC.md §4.2.
/// Represents a placed zone that can span multiple chunks.
/// </summary>
internal sealed class ZoneInstance
{
    /// <summary>
    /// Unique zone instance identifier.
    /// </summary>
    public int ZoneId { get; }

    /// <summary>
    /// Definition ID (e.g., "bedroom", "assembly").
    /// </summary>
    public string DefId { get; set; }

    /// <summary>
    /// User-assigned name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Priority level (0=Low, 1=Normal, 2=High, 3=Critical).
    /// Used for tie-breaking when multiple zones overlap.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Subtype: "auto", "indoor", "outdoor" (for Assembly auto-naming, deferred in v1).
    /// </summary>
    public string Subtype { get; set; }

    /// <summary>
    /// Z-level mode: "single" (v1), "range" (future).
    /// </summary>
    public string ZMode { get; set; }

    /// <summary>
    /// Home chunk where the zone was created (owns global properties).
    /// </summary>
    public ChunkKey HomeChunk { get; }

    /// <summary>
    /// Chunks that contain cells belonging to this zone.
    /// </summary>
    public ImmutableHashSet<ChunkKey> MemberChunks { get; private set; }

    /// <summary>
    /// Total number of member cells across all chunks.
    /// </summary>
    public int TotalCells { get; set; }

    /// <summary>
    /// Optional policy overrides.
    /// </summary>
    public ZonePolicies? Policies { get; set; }

    /// <summary>
    /// Zone enabled/disabled state.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Generation counter, incremented on any configuration change.
    /// </summary>
    public uint Generation { get; private set; }

    /// <summary>
    /// Tick when this zone was created.
    /// </summary>
    public ulong CreatedTick { get; }

    public ZoneInstance(int zoneId, string defId, string name, ChunkKey homeChunk, ulong createdTick)
    {
        ZoneId = zoneId;
        DefId = defId ?? throw new ArgumentNullException(nameof(defId));
        Name = name ?? $"Zone {zoneId}";
        HomeChunk = homeChunk;
        CreatedTick = createdTick;
        Priority = 0;
        Subtype = "auto";
        ZMode = "single";
        MemberChunks = ImmutableHashSet<ChunkKey>.Empty;
        TotalCells = 0;
        Enabled = true;
        Generation = 1;
    }

    /// <summary>
    /// Update member chunks when cells are added/removed.
    /// </summary>
    public void UpdateMemberChunks(IEnumerable<ChunkKey> chunks)
    {
        MemberChunks = chunks.ToImmutableHashSet();
        Generation++;
    }

    /// <summary>
    /// Get member chunks in stable spatial order.
    /// </summary>
    public IReadOnlyList<ChunkKey> GetMemberChunksSnapshot()
    {
        return MemberChunks
            .OrderBy(static chunk => chunk.Z)
            .ThenBy(static chunk => chunk.ChunkY)
            .ThenBy(static chunk => chunk.ChunkX)
            .ToArray();
    }

    /// <summary>
    /// Increment generation when configuration changes.
    /// </summary>
    public void IncrementGeneration()
    {
        Generation++;
    }
}
