using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    internal int ZoneId { get; }

    /// <summary>
    /// Display name for the zone.
    /// </summary>
    internal string Name { get; set; }

    /// <summary>
    /// Home chunk where the zone was created (owns global properties).
    /// </summary>
    internal ChunkKey HomeChunk { get; }

    /// <summary>
    /// Filter determining which items this zone accepts.
    /// </summary>
    internal StockpileFilter Filter { get; set; }

    /// <summary>
    /// Priority level (0=Low, 1=Normal, 2=High, 3=Critical).
    /// </summary>
    internal int Priority { get; set; }

    /// <summary>
    /// Target number of stacks to maintain.
    /// </summary>
    internal int TargetStacks { get; set; }

    /// <summary>
    /// Start pulling items when stack count falls below this threshold.
    /// </summary>
    internal int HysteresisLow { get; set; }

    /// <summary>
    /// Stop pulling items when stack count exceeds this threshold.
    /// </summary>
    internal int HysteresisHigh { get; set; }

    /// <summary>
    /// Generation counter, incremented on any configuration change.
    /// </summary>
    internal uint Generation { get; private set; }

    /// <summary>
    /// Tick when this zone was created.
    /// </summary>
    internal ulong CreatedTick { get; }

    /// <summary>
    /// Chunks that contain cells belonging to this zone (immutable after creation).
    /// </summary>
    internal ImmutableHashSet<ChunkKey> MemberChunks { get; private set; }

    internal StockpileZone(int zoneId, string name, ChunkKey homeChunk, ulong createdTick)
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

    internal StockpileZone(
        int zoneId,
        string name,
        ChunkKey homeChunk,
        StockpileFilter filter,
        int priority,
        int targetStacks,
        int hysteresisLow,
        int hysteresisHigh,
        uint generation,
        ulong createdTick,
        IEnumerable<ChunkKey> memberChunks)
    {
        ZoneId = zoneId;
        Name = name ?? $"Stockpile {zoneId}";
        HomeChunk = homeChunk;
        CreatedTick = createdTick;
        Filter = filter ?? new StockpileFilter();
        Priority = priority;
        TargetStacks = targetStacks;
        HysteresisLow = hysteresisLow;
        HysteresisHigh = hysteresisHigh;
        Generation = generation;
        MemberChunks = memberChunks.ToImmutableHashSet();
    }

    /// <summary>
    /// Update member chunks when cells are added/removed.
    /// </summary>
    internal void UpdateMemberChunks(IEnumerable<ChunkKey> chunks)
    {
        MemberChunks = chunks.ToImmutableHashSet();
        Generation++;
    }

    /// <summary>
    /// Get member chunks in stable spatial order.
    /// </summary>
    internal IReadOnlyList<ChunkKey> GetMemberChunksSnapshot()
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
    internal void IncrementGeneration()
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
