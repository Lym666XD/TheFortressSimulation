using System;
using SadRogue.Primitives;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Construction designation for L0 structural builds (wall/floor/ramp/stairs).
/// Supports multi-Z prisms per DF-style input: top Z places stairs down, bottom Z places stairs up, middle Z places UD.
/// </summary>
internal sealed class ConstructionDesignation
{
    internal readonly Rectangle WorldRect;
    internal readonly int ZMin;
    internal readonly int ZMax;
    internal readonly ConstructionShape Shape;
    internal readonly MaterialFilterSpec Filter; // reusable filtering/cache key
    internal readonly int Priority; // 0..100
    internal readonly ulong CreatedTick;

    internal ConstructionDesignation(
        Rectangle worldRect,
        int zMin,
        int zMax,
        ConstructionShape shape,
        MaterialFilterSpec filter,
        int priority,
        ulong createdTick)
    {
        if (zMin > zMax) throw new ArgumentOutOfRangeException(nameof(zMin));
        WorldRect = worldRect;
        ZMin = zMin;
        ZMax = zMax;
        Shape = shape;
        Filter = filter;
        Priority = priority;
        CreatedTick = createdTick;
    }
}

/// <summary>
/// Target structural kinds we support in L0 construction.
/// </summary>
internal enum ConstructionShape : byte
{
    Wall,
    Floor,
    Ramp,
    Stairs
}

/// <summary>
/// Planned build DTO emitted by ConstructionSystem.ReadTick.
/// Carries final target kind and optional geology handle selected by material resolver.
/// </summary>
internal readonly record struct PlannedBuild(
    Point Cell,
    int Z,
    TerrainKind TargetKind,
    ushort GeologyHandle,
    ConstructionShape Shape,
    string[] RequiredTags,
    int Priority,
    ulong Seed);

/// <summary>
/// Filter specification for selecting materials/items for construction.
/// Can be reused by L2 furniture and future workshop recipes.
/// </summary>
internal sealed class MaterialFilterSpec
{
    /// <summary>
    /// Optional concrete material ID (e.g., "core_mat_stone_granite"). If set, takes precedence.
    /// </summary>
    internal string? PreferredMaterialId { get; init; }

    /// <summary>
    /// Tag filters (e.g., ["construction","block","stone"]).
    /// </summary>
    internal string[] Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optional UI/cache category key (e.g., "l0.floor", "l0.wall").
    /// </summary>
    internal string CategoryKey { get; init; } = "l0.unknown";
}
