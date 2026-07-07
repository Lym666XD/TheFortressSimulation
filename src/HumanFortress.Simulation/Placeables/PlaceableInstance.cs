using System;
using System.Collections.Generic;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Simulation.Items;
using HumanFortress.Simulation.Items;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Runtime placeable instance stored in chunk.
/// Can be created from:
/// - Installable: ItemInstance with PlaceableProfile (preserves quality, material, decorations)
/// - Construction: Built on-site from ConstructionDefinition (quality always 0)
///
/// NOTE: PlaceableData is serialized to chunk saves when save system is implemented.
/// Currently stored in memory only via Chunk.PlaceableData layer.
/// </summary>
internal sealed partial class PlaceableInstance
{
    private const ulong UninstalledItemGuidScope = 0x554E494E53544954UL;

    // === IDENTITY ===
    /// <summary>
    /// Unique GUID for this placeable instance
    /// </summary>
    public Guid Guid { get; }

    /// <summary>
    /// Placeable kind (Installable from item, or Construction built on-site)
    /// </summary>
    public PlaceableKind Kind { get; }

    /// <summary>
    /// Definition ID (item def ID for Installable, construction def ID for Construction)
    /// </summary>
    public string DefinitionId { get; }

    // === LOCATION ===
    /// <summary>
    /// World position (anchor point, top-left for MVP)
    /// </summary>
    public Point Position { get; }

    /// <summary>
    /// Z-level
    /// </summary>
    public int Z { get; }

    /// <summary>
    /// Footprint dimensions (stored directly, no rotation in MVP)
    /// </summary>
    public Footprint Footprint { get; }

    // === SOURCE TRACKING (Installable only) ===
    /// <summary>
    /// Source item GUID (reference only, for uninstall tracking)
    /// Only set for Installable kind
    /// </summary>
    public Guid? SourceItemGuid { get; set; }

    /// <summary>
    /// Source item material ID (preserved for uninstall)
    /// Only set for Installable kind
    /// </summary>
    public string? SourceItemMaterial { get; set; }

    /// <summary>
    /// Source item quality tier (preserved for uninstall)
    /// Only set for Installable kind (-3 to +3)
    /// </summary>
    public int SourceItemQuality { get; set; }

    /// <summary>
    /// Source item decorations (inlays, engravings, sockets - preserved for uninstall)
    /// Only set for Installable kind
    /// </summary>
    public List<Improvement>? SourceItemDecorations { get; set; }

    /// <summary>
    /// Source item maker (crafter GUID - preserved for uninstall)
    /// Only set for Installable kind
    /// </summary>
    public Guid? SourceItemMaker { get; set; }

    // === EFFECTS (computed values, strategy B) ===
    /// <summary>
    /// Environmental effects (computed at install/build time)
    /// For Installable: base effects + quality modifier
    /// For Construction: fixed effects (quality always 0)
    /// </summary>
    public EffectsBlock Effects { get; set; } = new();

    /// <summary>
    /// Passability mode (Blocking/Nonblocking/Doorway). Defaults to Nonblocking for ghosts.
    /// </summary>
    public PassabilityMode Passability { get; set; } = PassabilityMode.Nonblocking;

    /// <summary>
    /// True if this is a temporary construction ghost placeholder.
    /// </summary>
    public bool IsGhost { get; set; } = false;

    /// <summary>
    /// Optional state when this instance represents a construction site.
    /// Tracks target, required materials, delivered materials (derived or cached), and build progress.
    /// </summary>
    public ConstructionSiteState? ConstructionSite { get; set; }

    /// <summary>
    /// Optional workshop state (set for completed workshop constructions).
    /// </summary>
    public WorkshopState? Workshop { get; set; }

    // === STATE MACHINES ===
    /// <summary>
    /// Door state (only if passability=doorway)
    /// </summary>
    public DoorState? DoorState { get; set; }

    // === OWNERSHIP ===
    /// <summary>
    /// Owner faction ID
    /// </summary>
    public string? OwnerFactionId { get; set; }

    /// <summary>
    /// Owner creature GUID
    /// </summary>
    public Guid? OwnerCreatureGuid { get; set; }

    /// <summary>
    /// Forbidden flag (blocks usage)
    /// </summary>
    public bool Forbidden { get; set; }

    // === CONDITION ===
    /// <summary>
    /// Current hit points
    /// </summary>
    public int HitPoints { get; set; }

    /// <summary>
    /// Maximum hit points (calculated from material and size)
    /// </summary>
    public int MaxHitPoints { get; set; }

    public PlaceableInstance(
        Guid guid,
        PlaceableKind kind,
        string definitionId,
        Point position,
        int z,
        Footprint footprint)
    {
        Guid = guid;
        Kind = kind;
        DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
        Position = position;
        Z = z;
        Footprint = footprint;
    }
}
