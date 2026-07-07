using System.Text.Json;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Simulation.Items;

namespace HumanFortress.Content.Definitions;

internal static partial class ItemDefinitionCatalogLoader
{
    private static List<ItemDefinition>? ParseDefinitions(
        string file,
        string json,
        List<string> messages,
        ref int failed)
    {
        List<ItemDefinition>? defs = null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("items", out var itemsElem))
            {
                var list = new List<ItemDefinition>();
                foreach (var elem in itemsElem.EnumerateArray())
                {
                    try
                    {
                        var def = ParseFurnitureItem(elem);
                        if (def != null)
                        {
                            list.Add(def);
                        }
                    }
                    catch (Exception ex)
                    {
                        messages.Add($"[ItemManager] ERROR: furniture entry invalid in {Path.GetFileName(file)}: {ex.Message}");
                        failed++;
                    }
                }

                defs = list;
            }
        }
        catch
        {
        }

        return defs ?? JsonSerializer.Deserialize<List<ItemDefinition>>(json, JsonOptions);
    }

    private static ItemDefinition? ParseFurnitureItem(JsonElement elem)
    {
        try
        {
            var def = new ItemDefinition
            {
                Id = elem.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty,
                Name = elem.TryGetProperty("name", out var nEl) ? nEl.GetString() : null,
                Kind = "placeable"
            };

            if (elem.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                var tags = new List<string>();
                foreach (var tag in tagsEl.EnumerateArray())
                {
                    if (tag.ValueKind == JsonValueKind.String)
                    {
                        tags.Add(tag.GetString()!);
                    }
                }

                def.Tags = tags.ToArray();
            }

            if (elem.TryGetProperty("base_volume_ml", out var volumeEl) && volumeEl.ValueKind == JsonValueKind.Number)
            {
                def.BaseVolumeML = volumeEl.GetInt32();
            }

            if (elem.TryGetProperty("base_mass_g", out var massEl) && massEl.ValueKind == JsonValueKind.Number)
            {
                def.BaseMassG = massEl.GetInt32();
            }

            if (elem.TryGetProperty("stack", out var stackEl) && stackEl.ValueKind == JsonValueKind.Object)
            {
                def.Stack = ParseStack(stackEl);
            }

            if (elem.TryGetProperty("placeable_profile", out var profileEl)
                && profileEl.ValueKind == JsonValueKind.Object)
            {
                def.PlaceableProfile = ParsePlaceableProfile(profileEl);
            }

            var tagsSet = new HashSet<string>(def.Tags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            tagsSet.Add("furniture");
            def.Tags = tagsSet.ToArray();
            return def;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private static StackBlock ParseStack(JsonElement stackEl)
    {
        var stack = new StackBlock();
        if (stackEl.TryGetProperty("mode", out var modeEl) && modeEl.ValueKind == JsonValueKind.String)
        {
            var mode = modeEl.GetString()?.ToLowerInvariant();
            stack.Mode = mode switch
            {
                "none" => StackMode.None,
                "charges" => StackMode.Charges,
                _ => StackMode.Count
            };
        }

        if (stackEl.TryGetProperty("unit", out var unitEl) && unitEl.ValueKind == JsonValueKind.String)
        {
            stack.Unit = unitEl.GetString();
        }

        if (stackEl.TryGetProperty("max_per_stack", out var maxPerStackEl)
            && maxPerStackEl.ValueKind == JsonValueKind.Number)
        {
            stack.MaxPerStack = maxPerStackEl.GetInt32();
        }

        return stack;
    }

    private static PlaceableProfile ParsePlaceableProfile(JsonElement profileEl)
    {
        var profile = new PlaceableProfile();
        if (profileEl.TryGetProperty("footprint", out var footprintEl)
            && footprintEl.ValueKind == JsonValueKind.Object)
        {
            int w = footprintEl.TryGetProperty("w", out var wEl) && wEl.ValueKind == JsonValueKind.Number
                ? wEl.GetInt32()
                : 1;
            int d = footprintEl.TryGetProperty("d", out var dEl) && dEl.ValueKind == JsonValueKind.Number
                ? dEl.GetInt32()
                : 1;
            int h = footprintEl.TryGetProperty("h", out var hEl) && hEl.ValueKind == JsonValueKind.Number
                ? hEl.GetInt32()
                : 1;
            profile.Footprint = new Footprint(w, d, h);
        }

        if (profileEl.TryGetProperty("passability", out var passabilityEl)
            && passabilityEl.ValueKind == JsonValueKind.String)
        {
            var passability = passabilityEl.GetString()?.ToLowerInvariant();
            profile.Passability = passability switch
            {
                "blocking" => PassabilityMode.Blocking,
                "doorway" => PassabilityMode.Doorway,
                _ => PassabilityMode.Nonblocking
            };
        }

        if (profileEl.TryGetProperty("requires_floor", out var requiresFloorEl)
            && (requiresFloorEl.ValueKind == JsonValueKind.True || requiresFloorEl.ValueKind == JsonValueKind.False))
        {
            profile.RequiresFloor = requiresFloorEl.GetBoolean();
        }

        if (profileEl.TryGetProperty("clearance_h", out var clearanceEl)
            && clearanceEl.ValueKind == JsonValueKind.Number)
        {
            profile.ClearanceH = clearanceEl.GetInt32();
        }

        if (profileEl.TryGetProperty("blocks_light", out var blocksLightEl)
            && (blocksLightEl.ValueKind == JsonValueKind.True || blocksLightEl.ValueKind == JsonValueKind.False))
        {
            profile.BlocksLight = blocksLightEl.GetBoolean();
        }

        if (profileEl.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
        {
            var tags = new List<string>();
            foreach (var tag in tagsEl.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String)
                {
                    tags.Add(tag.GetString()!);
                }
            }

            profile.Tags = tags.ToArray();
        }

        if (profileEl.TryGetProperty("effects", out var effectsEl)
            && effectsEl.ValueKind == JsonValueKind.Object)
        {
            profile.Effects = ParseEffects(effectsEl);
        }

        return profile;
    }

    private static EffectsBlock ParseEffects(JsonElement effectsEl)
    {
        var effects = new EffectsBlock();
        if (effectsEl.TryGetProperty("beauty", out var beautyEl) && beautyEl.ValueKind == JsonValueKind.Number)
        {
            effects.Beauty = beautyEl.GetInt32();
        }

        if (effectsEl.TryGetProperty("comfort", out var comfortEl) && comfortEl.ValueKind == JsonValueKind.Number)
        {
            effects.Comfort = comfortEl.GetInt32();
        }

        if (effectsEl.TryGetProperty("light_lumen", out var lightEl) && lightEl.ValueKind == JsonValueKind.Number)
        {
            effects.LightLumen = lightEl.GetInt32();
        }

        if (effectsEl.TryGetProperty("heat_w", out var heatEl) && heatEl.ValueKind == JsonValueKind.Number)
        {
            effects.HeatW = heatEl.GetInt32();
        }

        return effects;
    }
}
