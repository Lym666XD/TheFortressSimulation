using System.Collections.Generic;
using System.Text.Json;

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
    /// Load tuning from serialized tuning.placeable.json content. Falls back to defaults.
    /// </summary>
    public static PlaceableTuning LoadFromJson(string? json)
    {
        var t = Default;
        if (string.IsNullOrWhiteSpace(json)) return t;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
        }
        catch
        {
            return t;
        }

        using (document)
        {
            var obj = document.RootElement;
            if (obj.ValueKind != JsonValueKind.Object) return t;

            if (TryGetObject(obj, "quality", out var quality))
            {
                t.BeautyPerTier = GetInt(quality, "beauty_per_tier") ?? t.BeautyPerTier;
                t.ComfortPerTier = GetInt(quality, "comfort_per_tier") ?? t.ComfortPerTier;
                t.MinTier = GetInt(quality, "min_tier") ?? t.MinTier;
                t.MaxTier = GetInt(quality, "max_tier") ?? t.MaxTier;
            }

            if (TryGetObject(obj, "durability", out var durability))
            {
                t.DefaultMaxHP = GetInt(durability, "default_max_hp") ?? t.DefaultMaxHP;
                t.HPPerVolumeML = GetFloat(durability, "hp_per_volume_ml") ?? t.HPPerVolumeML;

                if (TryGetObject(durability, "material_hp_multiplier", out var materialHpMultiplier))
                {
                    t.MaterialHPMultiplier = ReadFloatDictionary(materialHpMultiplier);
                }

                if (TryGetObject(durability, "condition_thresholds", out var conditionThresholds))
                {
                    t.ConditionThresholds = ReadFloatDictionary(conditionThresholds);
                }
            }

            if (TryGetObject(obj, "installation", out var installation))
            {
                t.InstallTimeBaseTicks = GetInt(installation, "install_time_base_ticks") ?? t.InstallTimeBaseTicks;
                t.DeconstructTimeBaseTicks = GetInt(installation, "deconstruct_time_base_ticks") ?? t.DeconstructTimeBaseTicks;
                t.MaterialRecoveryRate = GetFloat(installation, "material_recovery_rate") ?? t.MaterialRecoveryRate;
                t.PreserveItemOnUninstall = GetBool(installation, "preserve_item_on_uninstall") ?? t.PreserveItemOnUninstall;
            }

            if (TryGetObject(obj, "construction", out var construction))
            {
                t.ConstructionQualityAlwaysZero = GetBool(construction, "quality_tier_always_zero") ?? t.ConstructionQualityAlwaysZero;
                t.SkillXPPerBuildTick = GetInt(construction, "skill_xp_per_build_tick") ?? t.SkillXPPerBuildTick;
            }

            if (TryGetObject(obj, "doors", out var doors))
            {
                t.DoorDefaultLocked = GetBool(doors, "default_locked") ?? t.DoorDefaultLocked;
                t.DoorDefaultOpen = GetBool(doors, "default_open") ?? t.DoorDefaultOpen;
                t.DoorOpenCostTicks = GetInt(doors, "open_cost_ticks") ?? t.DoorOpenCostTicks;
                t.DoorCloseCostTicks = GetInt(doors, "close_cost_ticks") ?? t.DoorCloseCostTicks;
                t.DoorClosedBlocksMovement = GetBool(doors, "closed_blocks_movement") ?? t.DoorClosedBlocksMovement;
            }

            if (TryGetObject(obj, "collision", out var collision))
            {
                t.CheckFullFootprint = GetBool(collision, "check_full_footprint") ?? t.CheckFullFootprint;
                t.RequireWalkableTiles = GetBool(collision, "require_walkable_tiles") ?? t.RequireWalkableTiles;
                t.AllowOverlapExternalRefs = GetBool(collision, "allow_overlap_external_refs") ?? t.AllowOverlapExternalRefs;
                t.CrossChunkValidation = GetBool(collision, "cross_chunk_validation") ?? t.CrossChunkValidation;
            }

            if (TryGetObject(obj, "workshops", out var workshops))
            {
                t.WorkersPerWorkshopMax = GetInt(workshops, "workers_per_workshop_max") ?? t.WorkersPerWorkshopMax;
            }
        }

        return t;
    }

    private static bool TryGetObject(JsonElement obj, string property, out JsonElement value)
    {
        return obj.TryGetProperty(property, out value) && value.ValueKind == JsonValueKind.Object;
    }

    private static int? GetInt(JsonElement obj, string property)
    {
        return obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
    }

    private static float? GetFloat(JsonElement obj, string property)
    {
        return obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetSingle()
            : null;
    }

    private static bool? GetBool(JsonElement obj, string property)
    {
        return obj.TryGetProperty(property, out var value)
            && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }

    private static Dictionary<string, float> ReadFloatDictionary(JsonElement obj)
    {
        var values = new Dictionary<string, float>();
        foreach (var property in obj.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
            {
                values[property.Name] = property.Value.GetSingle();
            }
        }

        return values;
    }
}
