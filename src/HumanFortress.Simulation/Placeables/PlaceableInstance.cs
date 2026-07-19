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
    // === IDENTITY ===
    /// <summary>
    /// Unique GUID for this placeable instance
    /// </summary>
    internal Guid Guid { get; }

    /// <summary>
    /// Placeable kind (Installable from item, or Construction built on-site)
    /// </summary>
    internal PlaceableKind Kind { get; }

    /// <summary>
    /// Definition ID (item def ID for Installable, construction def ID for Construction)
    /// </summary>
    internal string DefinitionId { get; }

    // === LOCATION ===
    /// <summary>
    /// World position (anchor point, top-left for MVP)
    /// </summary>
    internal Point Position { get; }

    /// <summary>
    /// Z-level
    /// </summary>
    internal int Z { get; }

    /// <summary>
    /// Footprint dimensions (stored directly, no rotation in MVP)
    /// </summary>
    internal Footprint Footprint { get; }

    // === SOURCE TRACKING (Installable only) ===
    /// <summary>
    /// Source item GUID (reference only, for uninstall tracking)
    /// Only set for Installable kind
    /// </summary>
    internal Guid? SourceItemGuid { get; set; }

    /// <summary>
    /// Source item material ID (preserved for uninstall)
    /// Only set for Installable kind
    /// </summary>
    internal string? SourceItemMaterial { get; set; }

    /// <summary>
    /// Source item quality tier (preserved for uninstall)
    /// Only set for Installable kind (-3 to +3)
    /// </summary>
    internal int SourceItemQuality { get; set; }

    /// <summary>
    /// Source item decorations (inlays, engravings, sockets - preserved for uninstall)
    /// Only set for Installable kind
    /// </summary>
    internal List<Improvement>? SourceItemDecorations { get; set; }

    /// <summary>
    /// Source item maker (crafter GUID - preserved for uninstall)
    /// Only set for Installable kind
    /// </summary>
    internal Guid? SourceItemMaker { get; set; }

    // === EFFECTS (computed values, strategy B) ===
    /// <summary>
    /// Environmental effects (computed at install/build time)
    /// For Installable: base effects + quality modifier
    /// For Construction: fixed effects (quality always 0)
    /// </summary>
    internal EffectsBlock Effects { get; set; } = new();

    /// <summary>
    /// Passability mode (Blocking/Nonblocking/Doorway). Defaults to Nonblocking for ghosts.
    /// </summary>
    internal PassabilityMode Passability { get; set; } = PassabilityMode.Nonblocking;

    /// <summary>
    /// True if this is a temporary construction ghost placeholder.
    /// </summary>
    internal bool IsGhost { get; set; } = false;

    /// <summary>
    /// Optional state when this instance represents a construction site.
    /// Tracks target, required materials, delivered materials (derived or cached), and build progress.
    /// </summary>
    internal ConstructionSiteState? ConstructionSite { get; set; }

    /// <summary>
    /// Optional workshop state (set for completed workshop constructions).
    /// </summary>
    internal WorkshopState? Workshop { get; set; }

    // === STATE MACHINES ===
    /// <summary>
    /// Door state (only if passability=doorway)
    /// </summary>
    internal DoorState? DoorState { get; private set; }

    // === OWNERSHIP ===
    /// <summary>
    /// Owner faction ID
    /// </summary>
    internal string? OwnerFactionId { get; set; }

    /// <summary>
    /// Owner creature GUID
    /// </summary>
    internal Guid? OwnerCreatureGuid { get; set; }

    /// <summary>
    /// Forbidden flag (blocks usage)
    /// </summary>
    internal bool Forbidden { get; set; }

    // === CONDITION ===
    /// <summary>
    /// Current hit points
    /// </summary>
    internal int HitPoints { get; set; }

    /// <summary>
    /// Maximum hit points (calculated from material and size)
    /// </summary>
    internal int MaxHitPoints { get; set; }

    internal PlaceableInstance(
        Guid guid,
        PlaceableKind kind,
        string definitionId,
        Point position,
        int z,
        Footprint footprint,
        DoorState? doorState = null)
    {
        Guid = guid;
        Kind = kind;
        DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
        Position = position;
        Z = z;
        Footprint = footprint;
        DoorState = doorState;
    }

    /// <summary>
    /// Called only by the Simulation topology transaction after the footprint's
    /// derived occupancy has been validated for the same committed state.
    /// </summary>
    internal void ApplyCommittedDoorState(DoorState doorState)
    {
        DoorState = doorState ?? throw new ArgumentNullException(nameof(doorState));
    }
}
