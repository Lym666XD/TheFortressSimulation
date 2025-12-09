using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using HumanFortress.Core.Content.Registry;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Static definition of an item type loaded from data/core/items/*.json
/// Based on ITEMS_SPEC.md v4-int
/// </summary>
public sealed class ItemDefinition
{
    // === CORE IDENTITY ===
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Desc { get; set; }
    public string Kind { get; set; } = "RESOURCE";  // string for flexibility, inferred from blocks
    public string[] Tags { get; set; } = Array.Empty<string>();

    // === MATERIAL REFERENCE ===
    /// <summary>
    /// Fixed material string ID (e.g. "core_mat_metal_iron")
    /// Use this for raw resources like ingots, blocks, planks
    /// Mutually exclusive with multi-material crafting (future)
    /// </summary>
    [JsonPropertyName("fixed_material")]
    public string? FixedMaterial { get; set; }

    // === GEOMETRY & MASS (all integer values per SPEC v4-int §4) ===
    /// <summary>
    /// Base volume in milliliters (mL), required
    /// </summary>
    [JsonPropertyName("base_volume_ml")]
    public int BaseVolumeML { get; set; }

    /// <summary>
    /// Base mass in grams (g), optional
    /// If null, mass is calculated from material density × volume
    /// Use this for items with air gaps or composite materials
    /// </summary>
    [JsonPropertyName("base_mass_g")]
    public int? BaseMassG { get; set; }

    /// <summary>
    /// Fixed mass in grams (g), optional
    /// Overrides material-based calculation (e.g. for feathers, balloons)
    /// </summary>
    [JsonPropertyName("fixed_mass_g")]
    public int? FixedMassG { get; set; }

    // === STACKING (per SPEC §10) ===
    public StackBlock? Stack { get; set; }

    // === FEATURE BLOCKS (MVP: null占位, 后续实现) ===
    public EquipBlock? Equip { get; set; }
    public WeaponBlock? Weapon { get; set; }
    public AmmoBlock? Ammo { get; set; }
    public ContainerBlock? Container { get; set; }
    public UseBlock? Use { get; set; }
    public PlaceableProfile? PlaceableProfile { get; set; }

    // === ECONOMY ===
    [JsonPropertyName("quality_tier")]
    public int? QualityTier { get; set; }  // -3..+3, default null = normal quality
    [JsonPropertyName("value_mul_fx")]
    public int ValueMulFx { get; set; } = 10000;  // FX multiplier, 10000 = 1.0×

    // === DURABILITY (MVP未实现) ===
    [JsonPropertyName("durability_max")]
    public int? DurabilityMax { get; set; }

    // === LEGACY COMPATIBILITY ===
    [Obsolete("Use Stack property instead")]
    public StackMode StackMode { get; set; } = StackMode.None;
    [Obsolete("Use Stack.MaxPerStack instead")]
    public int MaxPerStack { get; set; } = 1;
}

// === STACK BLOCK (per SPEC §10) ===
public class StackBlock
{
    public StackMode Mode { get; set; } = StackMode.Count;
    public string? Unit { get; set; }  // "piece", "g", "ml"
    [JsonPropertyName("max_per_stack")]
    public int MaxPerStack { get; set; } = 1;
    [JsonPropertyName("equal_when")]
    public string[] EqualWhen { get; set; } = Array.Empty<string>();  // ["fixed_material", "quality"]
    [JsonPropertyName("requires_pristine")]
    public bool RequiresPristine { get; set; } = false;
    [JsonPropertyName("require_no_mods")]
    public bool RequireNoMods { get; set; } = false;
    [JsonPropertyName("requires_empty")]
    public bool RequiresEmpty { get; set; } = false;
}

public enum StackMode
{
    None,
    Count,
    Charges
}

// === FEATURE BLOCKS (MVP占位类型) ===

public class EquipBlock
{
    public string Slot { get; set; } = "";  // "head", "torso", "hands", etc.
    public int EncumbranceFx { get; set; }  // FX integer
    public ArmorStats? Armor { get; set; }
    // ShapeMod will go here later
}

public class ArmorStats
{
    public int CoverageFx { get; set; }  // FX 0..10000 (0..100%)
    public int LayerThicknessMM { get; set; }  // millimeters
}

public class WeaponBlock
{
    public string WeaponType { get; set; } = "";  // "melee", "ranged"
    public DamageBlock Damage { get; set; } = new();
    public int ReachTiles { get; set; } = 1;
    // ShapeMod and bonuses will go here later
}

public class DamageBlock
{
    public string DamageType { get; set; } = "blunt";  // "blunt", "pierce", "slash"
    public int BaseDamageFx { get; set; }  // FX integer
}

public class AmmoBlock
{
    public string AmmoType { get; set; } = "";
    public int PenetrationFx { get; set; }
}

public class ContainerBlock
{
    public int CapacityML { get; set; }
    public string[] AllowedTags { get; set; } = Array.Empty<string>();
}

public class UseBlock
{
    public string UseVerb { get; set; } = "use";  // "eat", "drink", "apply"
    public int Charges { get; set; } = 1;
}

// Note: PlaceableProfile is now defined in HumanFortress.Core.Content.Registry namespace
// See PlaceableProfile.cs for full implementation with footprint, passability, effects, etc.