using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Represents a stockpile zone that can span multiple chunks.
/// Global definition per STOCKPILE_SPEC.md.
/// </summary>
internal sealed class StockpileZone
{
    /// <summary>
    /// Unique zone identifier.
    /// </summary>
    public int ZoneId { get; }

    /// <summary>
    /// Display name for the zone.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Home chunk where the zone was created (owns global properties).
    /// </summary>
    public ChunkKey HomeChunk { get; }

    /// <summary>
    /// Filter determining which items this zone accepts.
    /// </summary>
    public StockpileFilter Filter { get; set; }

    /// <summary>
    /// Priority level (0=Low, 1=Normal, 2=High, 3=Critical).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Target number of stacks to maintain.
    /// </summary>
    public int TargetStacks { get; set; }

    /// <summary>
    /// Start pulling items when stack count falls below this threshold.
    /// </summary>
    public int HysteresisLow { get; set; }

    /// <summary>
    /// Stop pulling items when stack count exceeds this threshold.
    /// </summary>
    public int HysteresisHigh { get; set; }

    /// <summary>
    /// Generation counter, incremented on any configuration change.
    /// </summary>
    public uint Generation { get; private set; }

    /// <summary>
    /// Tick when this zone was created.
    /// </summary>
    public ulong CreatedTick { get; }

    /// <summary>
    /// Chunks that contain cells belonging to this zone (immutable after creation).
    /// </summary>
    public ImmutableHashSet<ChunkKey> MemberChunks { get; private set; }

    public StockpileZone(int zoneId, string name, ChunkKey homeChunk, ulong createdTick)
    {
        ZoneId = zoneId;
        Name = name ?? $"Stockpile {zoneId}";
        HomeChunk = homeChunk;
        CreatedTick = createdTick;
        Filter = new StockpileFilter();
        Priority = 1; // Normal
        TargetStacks = 100;
        HysteresisLow = 70;
        HysteresisHigh = 90;
        Generation = 1;
        MemberChunks = ImmutableHashSet<ChunkKey>.Empty;
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
    /// Increment generation when configuration changes.
    /// </summary>
    public void IncrementGeneration()
    {
        Generation++;
    }
}

/// <summary>
/// Priority levels for stockpile zones.
/// </summary>
internal enum ZonePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}