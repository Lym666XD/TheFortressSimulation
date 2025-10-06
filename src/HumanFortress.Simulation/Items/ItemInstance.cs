using System;
using System.Collections.Generic;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Runtime instance of an item in the world
/// Based on ITEMS_SPEC v4-int §17
/// </summary>
public sealed class ItemInstance
{
    // === IDENTITY ===
    public Guid Guid { get; }
    public string DefinitionId { get; }

    /// <summary>
    /// Material string ID (references MaterialRegistry)
    /// Null if item uses fixed_material from definition
    /// </summary>
    public string? MaterialId { get; set; }

    public int StackCount { get; set; }

    // === LOCATION ===
    /// <summary>
    /// World position (always set, represents last known position even if carried/contained)
    /// Use IsOnGround to check if item is actually at this position
    /// </summary>
    public Point Position { get; set; }
    public int Z { get; set; }

    /// <summary>
    /// Container GUID (if inside container, Position becomes irrelevant)
    /// </summary>
    public Guid? ContainedBy { get; set; }

    /// <summary>
    /// Creature GUID (if in inventory, Position becomes irrelevant)
    /// </summary>
    public Guid? CarriedBy { get; set; }

    /// <summary>
    /// Creature GUID (if equipped, Position becomes irrelevant)
    /// </summary>
    public Guid? EquippedBy { get; set; }

    /// <summary>
    /// Placeable installation data (if installed as furniture/workstation)
    /// When set, item is installed at InstalledAt.AnchorWorld instead of Position
    /// </summary>
    public PlacementData? InstalledAt { get; set; }

    /// <summary>
    /// Helper: check if item is on ground (not carried/contained/equipped/installed)
    /// </summary>
    public bool IsOnGround => ContainedBy == null && CarriedBy == null && EquippedBy == null && InstalledAt == null;

    // === OWNERSHIP & ACCESS (per SPEC §17.8) ===
    public string? OwnerFactionId { get; set; }
    public Guid? OwnerCreatureGuid { get; set; }
    public UsePolicy UsePolicy { get; set; } = UsePolicy.Public;
    public bool Forbidden { get; set; } = false;

    /// <summary>
    /// Reservation tokens for job system (replaces old IsReserved/ReservedBy)
    /// Multiple jobs can reserve portions of a stack
    /// </summary>
    public List<ReservationToken> ReservationTokens { get; set; } = new();

    // === QUALITY & CONDITION ===
    public int QualityTier { get; set; } = 0;  // -3 to +3
    public bool Artifact { get; set; } = false;
    public string? ArtifactName { get; set; }
    public string ConditionState { get; set; } = "Pristine";  // Pristine/Good/Worn/Damaged/Broken
    public int? DurabilityCurrent { get; set; }
    public int? DurabilityMax { get; set; }

    // === PROVENANCE (only if QualityTier >= +3 or Artifact) ===
    public Guid? CraftedBy { get; set; }
    public string? MakerFactionId { get; set; }
    public string? StyleTag { get; set; }

    // === IMPROVEMENTS (decorations, enchantments) ===
    public List<Improvement>? Improvements { get; set; }

    // === PERISHABLE (food/drink only) ===
    public PerishableState? Perishable { get; set; }

    // === RUNTIME METADATA ===
    public ulong SpawnedAtTick { get; }

    // === LEGACY COMPATIBILITY ===
    [Obsolete("Use ReservationTokens instead")]
    public bool IsReserved { get; set; } = false;
    [Obsolete("Use ReservationTokens instead")]
    public Guid? ReservedBy { get; set; } = null;
    [Obsolete("Use CarriedBy/EquippedBy instead")]
    public bool IsCarried { get; set; } = false;

    public ItemInstance(Guid guid, string definitionId, Point position, int z, int stackCount, ulong spawnTick)
    {
        Guid = guid;
        DefinitionId = definitionId;
        Position = position;
        Z = z;
        StackCount = stackCount;
        SpawnedAtTick = spawnTick;
    }
}

// === SUPPORT TYPES ===

public enum UsePolicy
{
    Public,    // Anyone can use
    Faction,   // Only faction members
    Private    // Only owner
}

public class ReservationToken
{
    public Guid JobGuid { get; set; }
    public Guid? ClaimantCreatureGuid { get; set; }
    public int ReservedCount { get; set; }  // portion of stack reserved
    public ulong ExpiresAtTick { get; set; }
    public string ReservationType { get; set; } = "haul";  // haul/craft/consume
}

public class PlacementData
{
    public Point AnchorWorld { get; set; }
    public int Z { get; set; }
    public int Rotation { get; set; }  // 0=N, 1=E, 2=S, 3=W
    public string? StateId { get; set; }  // for multi-state placeables (doors)
}

public class Improvement
{
    public string Type { get; set; } = "";  // "engraving", "enchantment", "gem_inlay"
    public string? MaterialId { get; set; }
    public int QualityTier { get; set; }
    public Guid? CreatedBy { get; set; }
    public string? Description { get; set; }
}

public class PerishableState
{
    public ulong CreatedAtTick { get; set; }
    public int FreshDurationTicks { get; set; }
    public int SpoilDurationTicks { get; set; }
    public float CurrentFreshness { get; set; } = 1.0f;  // 1.0 = fresh, 0.0 = rotten
}
