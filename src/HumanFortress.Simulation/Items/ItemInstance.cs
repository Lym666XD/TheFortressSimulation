using System;
using System.Collections.Generic;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Runtime instance of an item in the world
/// Based on ITEMS_SPEC v4-int §17
/// </summary>
internal sealed class ItemInstance
{
    // === IDENTITY ===
    internal Guid Guid { get; }
    internal string DefinitionId { get; }

    /// <summary>
    /// Material string ID (references MaterialRegistry)
    /// Null if item uses fixed_material from definition
    /// </summary>
    internal string? MaterialId { get; set; }

    internal int StackCount { get; set; }

    // === LOCATION ===
    /// <summary>
    /// World position (always set, represents last known position even if carried/contained)
    /// Use IsOnGround to check if item is actually at this position
    /// </summary>
    internal Point Position { get; set; }
    internal int Z { get; set; }

    /// <summary>
    /// Container GUID (if inside container, Position becomes irrelevant)
    /// </summary>
    internal Guid? ContainedBy { get; set; }

    /// <summary>
    /// Creature GUID (if in inventory, Position becomes irrelevant)
    /// </summary>
    internal Guid? CarriedBy { get; set; }

    /// <summary>
    /// Creature GUID (if equipped, Position becomes irrelevant)
    /// </summary>
    internal Guid? EquippedBy { get; set; }

    /// <summary>
    /// Placeable installation data (if installed as furniture/workstation)
    /// When set, item is installed at InstalledAt.AnchorWorld instead of Position
    /// </summary>
    internal PlacementData? InstalledAt { get; set; }

    /// <summary>
    /// Helper: check if item is on ground (not carried/contained/equipped/installed)
    /// </summary>
    internal bool IsOnGround => ContainedBy == null && CarriedBy == null && EquippedBy == null && InstalledAt == null;

    // === OWNERSHIP & ACCESS (per SPEC §17.8) ===
    internal string? OwnerFactionId { get; set; }
    internal Guid? OwnerCreatureGuid { get; set; }
    internal UsePolicy UsePolicy { get; set; } = UsePolicy.Public;
    internal bool Forbidden { get; set; } = false;

    /// <summary>
    /// Reservation tokens for job system.
    /// Multiple jobs can reserve portions of a stack
    /// </summary>
    internal List<ReservationToken> ReservationTokens { get; set; } = new();

    // === QUALITY & CONDITION ===
    internal int QualityTier { get; set; } = 0;  // -3 to +3
    internal bool Artifact { get; set; } = false;
    internal string? ArtifactName { get; set; }
    internal string ConditionState { get; set; } = "Pristine";  // Pristine/Good/Worn/Damaged/Broken
    internal int? DurabilityCurrent { get; set; }
    internal int? DurabilityMax { get; set; }

    // === PROVENANCE (only if QualityTier >= +3 or Artifact) ===
    internal Guid? CraftedBy { get; set; }
    internal string? MakerFactionId { get; set; }
    internal string? StyleTag { get; set; }

    // === IMPROVEMENTS (decorations, enchantments) ===
    internal List<Improvement>? Improvements { get; set; }

    // === PERISHABLE (food/drink only) ===
    internal PerishableState? Perishable { get; set; }

    // === RUNTIME METADATA ===
    internal ulong SpawnedAtTick { get; }

    internal ItemInstance(Guid guid, string definitionId, Point position, int z, int stackCount, ulong spawnTick)
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

internal enum UsePolicy
{
    Public,    // Anyone can use
    Faction,   // Only faction members
    Private    // Only owner
}

internal class ReservationToken
{
    internal Guid JobGuid { get; set; }
    internal Guid? ClaimantCreatureGuid { get; set; }
    internal int ReservedCount { get; set; }  // portion of stack reserved
    internal ulong ExpiresAtTick { get; set; }
    internal string ReservationType { get; set; } = "haul";  // haul/craft/consume
}

internal class PlacementData
{
    internal Point AnchorWorld { get; set; }
    internal int Z { get; set; }
    internal int Rotation { get; set; }  // 0=N, 1=E, 2=S, 3=W
    internal string? StateId { get; set; }  // for multi-state placeables (doors)
}

internal class Improvement
{
    internal string Type { get; set; } = "";  // "engraving", "enchantment", "gem_inlay"
    internal string? MaterialId { get; set; }
    internal int QualityTier { get; set; }
    internal Guid? CreatedBy { get; set; }
    internal string? Description { get; set; }
}

internal class PerishableState
{
    internal ulong CreatedAtTick { get; set; }
    internal int FreshDurationTicks { get; set; }
    internal int SpoilDurationTicks { get; set; }
    internal float CurrentFreshness { get; set; } = 1.0f;  // 1.0 = fresh, 0.0 = rotten
}
