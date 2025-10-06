using System;
using SadRogue.Primitives;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Construction designation for L0 structural builds (wall/floor/ramp/stairs).
/// Supports multi-Z prisms per DF-style input: top Z places stairs down, bottom Z places stairs up, middle Z places UD.
/// </summary>
public sealed class ConstructionDesignation
{
    public readonly Rectangle WorldRect;
    public readonly int ZMin;
    public readonly int ZMax;
    public readonly ConstructionShape Shape;
    public readonly MaterialFilterSpec Filter; // reusable filtering/cache key
    public readonly int Priority; // 0..100
    public readonly ulong CreatedTick;

    public ConstructionDesignation(
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
public enum ConstructionShape : byte
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
public readonly record struct PlannedBuild(
    Point Cell,
    int Z,
    TerrainKind TargetKind,
    ushort GeologyHandle,
    ConstructionShape Shape,
    int Priority,
    ulong Seed);

/// <summary>
/// Filter specification for selecting materials/items for construction.
/// Can be reused by L2 furniture and future workshop recipes.
/// </summary>
public sealed class MaterialFilterSpec
{
    /// <summary>
    /// Optional concrete material ID (e.g., "core_mat_stone_granite"). If set, takes precedence.
    /// </summary>
    public string? PreferredMaterialId { get; init; }

    /// <summary>
    /// Tag filters (e.g., ["construction","block","stone"]).
    /// </summary>
    public string[] Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optional UI/cache category key (e.g., "l0.floor", "l0.wall").
    /// </summary>
    public string CategoryKey { get; init; } = "l0.unknown";
}

