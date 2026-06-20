using System;

namespace HumanFortress.Contracts.Content.Registry;

/// <summary>
/// Placeable profile for items and constructions.
/// Defines footprint, passability, effects, and placement rules.
/// Shared between ItemDefinition.PlaceableProfile and ConstructionDefinition.PlaceableProfile.
/// </summary>
public sealed class PlaceableProfile
{
    // === GEOMETRY ===
    /// <summary>
    /// Footprint dimensions (no rotation support in MVP)
    /// </summary>
    public Footprint Footprint { get; set; }

    // === PASSABILITY ===
    /// <summary>
    /// Pathfinding passability mode
    /// </summary>
    public PassabilityMode Passability { get; set; }

    // === PLACEMENT RULES ===
    /// <summary>
    /// Whether this placeable requires a floor tile beneath it
    /// </summary>
    public bool RequiresFloor { get; set; }

    /// <summary>
    /// Required vertical clearance in tiles above the footprint
    /// </summary>
    public int ClearanceH { get; set; }

    /// <summary>
    /// Whether this placeable blocks light propagation
    /// </summary>
    public bool BlocksLight { get; set; }

    // === TAGS ===
    /// <summary>
    /// Tags for placeable-specific filtering (e.g., room requirements, "furniture_bed", "workshop_masonry")
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays (JSON deserialization)
    public string[] Tags { get; set; } = Array.Empty<string>();
#pragma warning restore CA1819

    // === EFFECTS ===
    /// <summary>
    /// Environmental effects (base values, quality modifier applied at install time for items)
    /// </summary>
    public EffectsBlock Effects { get; set; } = new();

    public PlaceableProfile Clone()
    {
        return new PlaceableProfile
        {
            Footprint = Footprint,
            Passability = Passability,
            RequiresFloor = RequiresFloor,
            ClearanceH = ClearanceH,
            BlocksLight = BlocksLight,
            Tags = (string[])Tags.Clone(),
            Effects = Effects.Clone()
        };
    }
}

/// <summary>
/// Footprint dimensions in tiles (W x D x H).
/// No rotation support in MVP - all placeables have fixed orientation.
/// </summary>
public struct Footprint
{
    /// <summary>
    /// Width (x-axis, tiles)
    /// </summary>
    public int W { get; set; }

    /// <summary>
    /// Depth (y-axis, tiles)
    /// </summary>
    public int D { get; set; }

    /// <summary>
    /// Height (z-axis, tiles)
    /// </summary>
    public int H { get; set; }

    public Footprint(int w, int d, int h)
    {
        W = w;
        D = d;
        H = h;
    }

    public override string ToString() => $"{W}×{D}×{H}";
}

/// <summary>
/// Passability mode for pathfinding integration
/// </summary>
public enum PassabilityMode
{
    /// <summary>
    /// Wall-like, completely blocks movement and line of sight
    /// </summary>
    Blocking,

    /// <summary>
    /// Passable, can walk through freely
    /// </summary>
    Nonblocking,

    /// <summary>
    /// Door-like, passability depends on open/closed state (requires DoorState component)
    /// </summary>
    Doorway
}

/// <summary>
/// Environmental effects block (beauty, comfort, light, heat).
/// Base values from definition, modified by quality for installable items.
/// </summary>
public sealed class EffectsBlock
{
    /// <summary>
    /// Beauty value (discrete integer, can be negative)
    /// </summary>
    public int Beauty { get; set; }

    /// <summary>
    /// Comfort value (discrete integer, non-negative)
    /// </summary>
    public int Comfort { get; set; }

    /// <summary>
    /// Light output in lumens (discrete integer)
    /// </summary>
    public int LightLumen { get; set; }

    /// <summary>
    /// Heat output in watts (discrete integer)
    /// </summary>
    public int HeatW { get; set; }

    public EffectsBlock Clone()
    {
        return new EffectsBlock
        {
            Beauty = Beauty,
            Comfort = Comfort,
            LightLumen = LightLumen,
            HeatW = HeatW
        };
    }

    public override string ToString() => $"Beauty:{Beauty} Comfort:{Comfort} Light:{LightLumen}lm Heat:{HeatW}W";
}
