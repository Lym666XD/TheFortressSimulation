using System;
using System.Text.Json;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Registry;

/// <summary>
/// Parser for material definitions: supports authoring and runtime formats.
/// Per MATERIALS_SPEC v4-min, authoring uses human-readable values and runtime uses FX integers.
/// </summary>
internal static class MaterialParser
{
    /// <summary>
    /// Parse material from JSON element (auto-detects authoring vs runtime format)
    /// </summary>
    internal static MaterialDefinition ParseMaterial(JsonElement elem, bool isAuthoringFormat = true)
    {
        var material = new MaterialDefinition();

        // Parse string ID (authoring format)
        if (elem.TryGetProperty("id", out var idElem))
        {
            var idStr = idElem.GetString();
            if (!string.IsNullOrEmpty(idStr))
            {
                material.StringId = idStr;
                // Numeric ID will be assigned by MaterialRegistry.LoadMaterials()
            }
        }

        // Parse display name (optional, defaults to StringId if not provided)
        if (elem.TryGetProperty("name", out var nameElem))
        {
            material.Name = nameElem.GetString() ?? material.StringId;
        }
        else
        {
            material.Name = material.StringId;
        }

        // Parse aliases
        if (elem.TryGetProperty("aliases", out var aliases))
        {
            foreach (var alias in aliases.EnumerateArray())
            {
                material.Aliases.Add(alias.GetString() ?? "");
            }
        }

        // Parse category (optional, can be inferred from tags)
        if (elem.TryGetProperty("category", out var category))
        {
            // Store in tags for now
            var catStr = category.GetString();
            if (!string.IsNullOrEmpty(catStr))
                material.Tags.Add(catStr);
        }

        // Parse tags
        if (elem.TryGetProperty("tags", out var tags))
        {
            foreach (var tag in tags.EnumerateArray())
            {
                material.Tags.Add(tag.GetString() ?? "");
            }
        }

        // Parse display
        if (elem.TryGetProperty("display", out var display))
        {
            material.Display = ParseDisplay(display);
        }

        // === v4 CORE FIELDS ===

        // Density (always in physical units, never FX)
        if (elem.TryGetProperty("density_solid", out var densitySolid))
        {
            material.DensitySolid = densitySolid.GetInt32();
        }
        else if (elem.TryGetProperty("densitySolid", out var densitySolidCamel))
        {
            material.DensitySolid = densitySolidCamel.GetInt32();
        }

        // Mechanics (the CORE v4 properties)
        material.Mechanics = ParseMechanics(elem, isAuthoringFormat);

        // Electric & Magic
        material.ElectricMagic = ParseElectricMagic(elem, isAuthoringFormat);

        // Economy
        material.Economy = ParseEconomy(elem, isAuthoringFormat);

        // Work
        material.Work = ParseWork(elem, isAuthoringFormat);

        // Phase (metadata)
        if (elem.TryGetProperty("phase", out var phase))
        {
            material.Phase = phase.GetString();
        }

        // Navigation
        material.Navigation = ParseNavigation(elem, isAuthoringFormat);

        // Infer category from tags
        material.Category = InferCategoryFromTags(material.Tags);

        return material;
    }

    private static MaterialMechanics ParseMechanics(JsonElement elem, bool isAuthoring)
    {
        var mechanics = new MaterialMechanics();

        // Try v4 field names first
        if (elem.TryGetProperty("hardness_edge", out var hardnessEdge))
        {
            mechanics.HardnessEdgeFx = isAuthoring
                ? FixedPoint.FromPct(hardnessEdge.GetDouble())
                : hardnessEdge.GetInt32();
        }
        else if (elem.TryGetProperty("hardnessEdge", out var hardnessEdgeCamel))
        {
            mechanics.HardnessEdgeFx = isAuthoring
                ? FixedPoint.FromPct(hardnessEdgeCamel.GetDouble())
                : hardnessEdgeCamel.GetInt32();
        }
        else if (elem.TryGetProperty("hardnessEdgeFx", out var hardnessEdgeFx))
        {
            mechanics.HardnessEdgeFx = hardnessEdgeFx.GetInt32();
        }

        if (elem.TryGetProperty("toughness_frac", out var toughnessFrac))
        {
            mechanics.ToughnessFracFx = isAuthoring
                ? FixedPoint.FromPct(toughnessFrac.GetDouble())
                : toughnessFrac.GetInt32();
        }
        else if (elem.TryGetProperty("toughnessFrac", out var toughnessFracCamel))
        {
            mechanics.ToughnessFracFx = isAuthoring
                ? FixedPoint.FromPct(toughnessFracCamel.GetDouble())
                : toughnessFracCamel.GetInt32();
        }
        else if (elem.TryGetProperty("toughnessFracFx", out var toughnessFracFx))
        {
            mechanics.ToughnessFracFx = toughnessFracFx.GetInt32();
        }

        if (elem.TryGetProperty("rigidity", out var rigidity))
        {
            mechanics.RigidityFx = isAuthoring
                ? FixedPoint.FromPct(rigidity.GetDouble())
                : rigidity.GetInt32();
        }
        else if (elem.TryGetProperty("rigidityFx", out var rigidityFx))
        {
            mechanics.RigidityFx = rigidityFx.GetInt32();
        }

        return mechanics;
    }

    private static MaterialElectricMagic ParseElectricMagic(JsonElement elem, bool isAuthoring)
    {
        var em = new MaterialElectricMagic();

        if (elem.TryGetProperty("electric_category", out var electricCategory) ||
            elem.TryGetProperty("electricCategory", out electricCategory))
        {
            var catStr = electricCategory.GetString()?.ToUpperInvariant();
            em.ElectricCategory = catStr switch
            {
                "CONDUCTOR" => ElectricCategory.Conductor,
                "INSULATOR" => ElectricCategory.Insulator,
                "SEMI" => ElectricCategory.Semi,
                _ => ElectricCategory.Semi
            };
        }

        if (elem.TryGetProperty("mana_conductivity", out var manaCond))
        {
            em.ManaConductivityFx = isAuthoring
                ? FixedPoint.FromPct(manaCond.GetDouble())
                : manaCond.GetInt32();
        }
        else if (elem.TryGetProperty("manaConductivity", out var manaCondCamel))
        {
            em.ManaConductivityFx = isAuthoring
                ? FixedPoint.FromPct(manaCondCamel.GetDouble())
                : manaCondCamel.GetInt32();
        }
        else if (elem.TryGetProperty("manaConductivityFx", out var manaCondFx))
        {
            em.ManaConductivityFx = manaCondFx.GetInt32();
        }

        return em;
    }

    private static MaterialEconomy ParseEconomy(JsonElement elem, bool isAuthoring)
    {
        var econ = new MaterialEconomy();

        if (elem.TryGetProperty("value_mul", out var valueMul))
        {
            econ.ValueMulFx = isAuthoring
                ? FixedPoint.FromFloat(valueMul.GetDouble())
                : valueMul.GetInt32();
        }
        else if (elem.TryGetProperty("valueMul", out var valueMulCamel))
        {
            econ.ValueMulFx = isAuthoring
                ? FixedPoint.FromFloat(valueMulCamel.GetDouble())
                : valueMulCamel.GetInt32();
        }
        else if (elem.TryGetProperty("valueMulFx", out var valueMulFx))
        {
            econ.ValueMulFx = valueMulFx.GetInt32();
        }

        if (elem.TryGetProperty("beauty_mul", out var beautyMul))
        {
            econ.BeautyMulFx = isAuthoring
                ? FixedPoint.FromFloat(beautyMul.GetDouble())
                : beautyMul.GetInt32();
        }
        else if (elem.TryGetProperty("beautyMul", out var beautyMulCamel))
        {
            econ.BeautyMulFx = isAuthoring
                ? FixedPoint.FromFloat(beautyMulCamel.GetDouble())
                : beautyMulCamel.GetInt32();
        }
        else if (elem.TryGetProperty("beautyMulFx", out var beautyMulFx))
        {
            econ.BeautyMulFx = beautyMulFx.GetInt32();
        }

        return econ;
    }

    private static MaterialWork ParseWork(JsonElement elem, bool isAuthoring)
    {
        var work = new MaterialWork();

        if (elem.TryGetProperty("work", out var workElem))
        {
            if (workElem.TryGetProperty("forgeable", out var forgeable))
                work.Forgeable = forgeable.GetBoolean();

            if (workElem.TryGetProperty("weldable", out var weldable))
                work.Weldable = weldable.GetBoolean();

            if (workElem.TryGetProperty("carveable", out var carveable))
                work.Carveable = carveable.GetBoolean();

            if (workElem.TryGetProperty("process_difficulty_mul", out var procDiff))
            {
                work.ProcessDifficultyMulFx = isAuthoring
                    ? FixedPoint.FromFloat(procDiff.GetDouble())
                    : procDiff.GetInt32();
            }
            else if (workElem.TryGetProperty("processDifficultyMul", out var procDiffCamel))
            {
                work.ProcessDifficultyMulFx = isAuthoring
                    ? FixedPoint.FromFloat(procDiffCamel.GetDouble())
                    : procDiffCamel.GetInt32();
            }
            else if (workElem.TryGetProperty("processDifficultyMulFx", out var procDiffFx))
            {
                work.ProcessDifficultyMulFx = procDiffFx.GetInt32();
            }
        }

        return work;
    }

    private static MaterialNavigation ParseNavigation(JsonElement elem, bool isAuthoring)
    {
        var nav = new MaterialNavigation();

        if (elem.TryGetProperty("navigation", out var navElem))
        {
            if (navElem.TryGetProperty("moveCostAdd", out var moveCostAdd) ||
                navElem.TryGetProperty("move_cost_add", out moveCostAdd))
            {
                nav.MoveCostAdd = moveCostAdd.GetInt32();
            }

            if (navElem.TryGetProperty("frictionMulFx", out var frictionMulFx))
            {
                nav.FrictionMulFx = frictionMulFx.GetInt32();
            }
            else if (navElem.TryGetProperty("friction_mul", out var frictionMul))
            {
                nav.FrictionMulFx = isAuthoring
                    ? FixedPoint.FromFloat(frictionMul.GetDouble())
                    : frictionMul.GetInt32();
            }
            else if (navElem.TryGetProperty("frictionMul", out var frictionMulCamel))
            {
                nav.FrictionMulFx = isAuthoring
                    ? FixedPoint.FromFloat(frictionMulCamel.GetDouble())
                    : frictionMulCamel.GetInt32();
            }

            if (navElem.TryGetProperty("hazardLevel", out var hazardLevel))
                nav.HazardLevel = hazardLevel.GetInt32();

            if (navElem.TryGetProperty("hazardType", out var hazardType))
                nav.HazardType = hazardType.GetString() ?? "none";
        }

        return nav;
    }

    private static DisplayProperties ParseDisplay(JsonElement elem)
    {
        var props = new DisplayProperties();

        if (elem.TryGetProperty("glyph", out var glyph))
        {
            var str = glyph.GetString();
            if (!string.IsNullOrEmpty(str))
                props.Glyph = str[0];
        }

        if (elem.TryGetProperty("foreground", out var fg))
        {
            if (Enum.TryParse<ConsoleColor>(fg.GetString(), out var color))
                props.Foreground = color;
        }

        if (elem.TryGetProperty("background", out var bg))
        {
            if (Enum.TryParse<ConsoleColor>(bg.GetString(), out var color))
                props.Background = color;
        }

        return props;
    }

    /// <summary>
    /// Infer material category from tags for backward compatibility
    /// </summary>
    private static string InferCategoryFromTags(HashSet<string> tags)
    {
        if (tags.Contains("ore")) return "ore";
        if (tags.Contains("stone")) return "stone";
        if (tags.Contains("metal")) return "metal";
        if (tags.Contains("wood")) return "wood";
        if (tags.Contains("soil")) return "soil";
        if (tags.Contains("mineral")) return "mineral";
        if (tags.Contains("gem")) return "gem";
        if (tags.Contains("glass")) return "glass";
        if (tags.Contains("ceramic")) return "ceramic";
        if (tags.Contains("organic")) return "organic";
        if (tags.Contains("liquid")) return "liquid";
        if (tags.Contains("gas")) return "gas";

        return "generic";
    }
}
