using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Tunable placeable system parameters from tuning.placeable.json.
/// Per PLACEABLE_SPEC.md.
/// </summary>
public sealed class PlaceableTuning
{
    // === QUALITY ===
    /// <summary>
    /// Beauty effect per quality tier (-3 to +3)
    /// </summary>
    public int BeautyPerTier { get; set; } = 1;

    /// <summary>
    /// Comfort effect per quality tier (-3 to +3)
    /// </summary>
    public int ComfortPerTier { get; set; } = 1;

    /// <summary>
    /// Minimum quality tier
    /// </summary>
    public int MinTier { get; set; } = -3;

    /// <summary>
    /// Maximum quality tier
    /// </summary>
    public int MaxTier { get; set; } = 3;

    // === DURABILITY ===
    /// <summary>
    /// Default maximum hit points for placeables
    /// </summary>
    public int DefaultMaxHP { get; set; } = 100;

    /// <summary>
    /// Hit points per milliliter of volume
    /// </summary>
    public float HPPerVolumeML { get; set; } = 0.001f;

    /// <summary>
    /// Material HP multipliers (stone=2.0, metal=3.0, wood=1.0, default=1.0)
    /// </summary>
    public Dictionary<string, float> MaterialHPMultiplier { get; set; } = new()
    {
        { "stone", 2.0f },
        { "metal", 3.0f },
        { "wood", 1.0f },
        { "default", 1.0f }
    };

    /// <summary>
    /// Condition state thresholds (pristine=1.0, good=0.9, worn=0.7, etc.)
    /// </summary>
    public Dictionary<string, float> ConditionThresholds { get; set; } = new()
    {
        { "pristine", 1.0f },
        { "good", 0.9f },
        { "worn", 0.7f },
        { "damaged", 0.5f },
        { "poor", 0.2f },
        { "terrible", 0.0f }
    };

    // === INSTALLATION ===
    /// <summary>
    /// Base installation time in ticks
    /// </summary>
    public int InstallTimeBaseTicks { get; set; } = 200;

    /// <summary>
    /// Base deconstruction time in ticks
    /// </summary>
    public int DeconstructTimeBaseTicks { get; set; } = 150;

    /// <summary>
    /// Material recovery rate on deconstruction (0.0 to 1.0)
    /// </summary>
    public float MaterialRecoveryRate { get; set; } = 1.0f;

    /// <summary>
    /// Whether to preserve item properties on uninstall
    /// </summary>
    public bool PreserveItemOnUninstall { get; set; } = true;

    // === CONSTRUCTION ===
    /// <summary>
    /// Whether construction quality tier is always zero
    /// </summary>
    public bool ConstructionQualityAlwaysZero { get; set; } = true;

    /// <summary>
    /// Skill XP gained per build tick
    /// </summary>
    public int SkillXPPerBuildTick { get; set; } = 1;

    // === DOORS ===
    /// <summary>
    /// Default locked state for doors
    /// </summary>
    public bool DoorDefaultLocked { get; set; } = false;

    /// <summary>
    /// Default open state for doors
    /// </summary>
    public bool DoorDefaultOpen { get; set; } = false;

    /// <summary>
    /// Time cost to open a door (ticks)
    /// </summary>
    public int DoorOpenCostTicks { get; set; } = 20;

    /// <summary>
    /// Time cost to close a door (ticks)
    /// </summary>
    public int DoorCloseCostTicks { get; set; } = 20;

    /// <summary>
    /// Whether closed doors block movement
    /// </summary>
    public bool DoorClosedBlocksMovement { get; set; } = true;

    // === COLLISION ===
    /// <summary>
    /// Whether to check full footprint for collision (vs. just anchor cell)
    /// </summary>
    public bool CheckFullFootprint { get; set; } = true;

    /// <summary>
    /// Whether placement requires all tiles to be walkable
    /// </summary>
    public bool RequireWalkableTiles { get; set; } = true;

    /// <summary>
    /// Whether to allow overlap with external references
    /// </summary>
    public bool AllowOverlapExternalRefs { get; set; } = false;

    /// <summary>
    /// Whether to perform cross-chunk validation
    /// </summary>
    public bool CrossChunkValidation { get; set; } = true;

    // === WORKSHOPS (UI/behavior helpers) ===
    /// <summary>
    /// Maximum workers per workshop (UI display and soft cap)
    /// </summary>
    public int WorkersPerWorkshopMax { get; set; } = 25;

    /// <summary>
    /// Get default tuning values.
    /// </summary>
    public static PlaceableTuning Default => new();

    /// <summary>
    /// Load tuning from content registries (tuning.placeable.json). Falls back to defaults.
    /// </summary>
    public static PlaceableTuning LoadFromContent()
    {
        var t = Default;
        var obj = ContentRegistry.Instance.GetTuning<JObject>("tuning.placeable", "$");
        if (obj == null) return t;

        // Quality
        var quality = obj["quality"] as JObject;
        if (quality != null)
        {
            t.BeautyPerTier = quality["beauty_per_tier"]?.Value<int?>() ?? t.BeautyPerTier;
            t.ComfortPerTier = quality["comfort_per_tier"]?.Value<int?>() ?? t.ComfortPerTier;
            t.MinTier = quality["min_tier"]?.Value<int?>() ?? t.MinTier;
            t.MaxTier = quality["max_tier"]?.Value<int?>() ?? t.MaxTier;
        }

        // Durability
        var durability = obj["durability"] as JObject;
        if (durability != null)
        {
            t.DefaultMaxHP = durability["default_max_hp"]?.Value<int?>() ?? t.DefaultMaxHP;
            t.HPPerVolumeML = durability["hp_per_volume_ml"]?.Value<float?>() ?? t.HPPerVolumeML;

            var matMult = durability["material_hp_multiplier"] as JObject;
            if (matMult != null)
            {
                t.MaterialHPMultiplier = new Dictionary<string, float>();
                foreach (var prop in matMult.Properties())
                {
                    t.MaterialHPMultiplier[prop.Name] = prop.Value.Value<float>();
                }
            }

            var condThresh = durability["condition_thresholds"] as JObject;
            if (condThresh != null)
            {
                t.ConditionThresholds = new Dictionary<string, float>();
                foreach (var prop in condThresh.Properties())
                {
                    t.ConditionThresholds[prop.Name] = prop.Value.Value<float>();
                }
            }
        }

        // Installation
        var installation = obj["installation"] as JObject;
        if (installation != null)
        {
            t.InstallTimeBaseTicks = installation["install_time_base_ticks"]?.Value<int?>() ?? t.InstallTimeBaseTicks;
            t.DeconstructTimeBaseTicks = installation["deconstruct_time_base_ticks"]?.Value<int?>() ?? t.DeconstructTimeBaseTicks;
            t.MaterialRecoveryRate = installation["material_recovery_rate"]?.Value<float?>() ?? t.MaterialRecoveryRate;
            t.PreserveItemOnUninstall = installation["preserve_item_on_uninstall"]?.Value<bool?>() ?? t.PreserveItemOnUninstall;
        }

        // Construction
        var construction = obj["construction"] as JObject;
        if (construction != null)
        {
            t.ConstructionQualityAlwaysZero = construction["quality_tier_always_zero"]?.Value<bool?>() ?? t.ConstructionQualityAlwaysZero;
            t.SkillXPPerBuildTick = construction["skill_xp_per_build_tick"]?.Value<int?>() ?? t.SkillXPPerBuildTick;
        }

        // Doors
        var doors = obj["doors"] as JObject;
        if (doors != null)
        {
            t.DoorDefaultLocked = doors["default_locked"]?.Value<bool?>() ?? t.DoorDefaultLocked;
            t.DoorDefaultOpen = doors["default_open"]?.Value<bool?>() ?? t.DoorDefaultOpen;
            t.DoorOpenCostTicks = doors["open_cost_ticks"]?.Value<int?>() ?? t.DoorOpenCostTicks;
            t.DoorCloseCostTicks = doors["close_cost_ticks"]?.Value<int?>() ?? t.DoorCloseCostTicks;
            t.DoorClosedBlocksMovement = doors["closed_blocks_movement"]?.Value<bool?>() ?? t.DoorClosedBlocksMovement;
        }

        // Collision
        var collision = obj["collision"] as JObject;
        if (collision != null)
        {
            t.CheckFullFootprint = collision["check_full_footprint"]?.Value<bool?>() ?? t.CheckFullFootprint;
            t.RequireWalkableTiles = collision["require_walkable_tiles"]?.Value<bool?>() ?? t.RequireWalkableTiles;
            t.AllowOverlapExternalRefs = collision["allow_overlap_external_refs"]?.Value<bool?>() ?? t.AllowOverlapExternalRefs;
            t.CrossChunkValidation = collision["cross_chunk_validation"]?.Value<bool?>() ?? t.CrossChunkValidation;
        }

        // Workshops
        var workshops = obj["workshops"] as JObject;
        if (workshops != null)
        {
            t.WorkersPerWorkshopMax = workshops["workers_per_workshop_max"]?.Value<int?>() ?? t.WorkersPerWorkshopMax;
        }

        return t;
    }
}
