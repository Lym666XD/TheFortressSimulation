using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Definitions;

internal static partial class CoreDataRegistryLoader
{
    private static ConstructionContentLoadResult LoadBuildableConstructions(string coreDataPath)
    {
        var messages = new List<string>();
        var files = new List<string>();
        var workshopsDir = Path.Combine(coreDataPath, "workshops");
        if (!Directory.Exists(workshopsDir))
        {
            messages.Add($"workshops dir not found: {workshopsDir}");
        }
        else
        {
            foreach (var file in Directory.GetFiles(workshopsDir, "core_workshop_*.json", SearchOption.TopDirectoryOnly))
            {
                files.Add(file);
            }
        }

        var legacyWorkshopPath = Path.Combine(coreDataPath, "placeable", "workshops.json");
        if (files.Count == 0 && File.Exists(legacyWorkshopPath))
        {
            files.Add(legacyWorkshopPath);
            messages.Add("loading legacy workshops.json from placeable dir");
        }

        var placeableDirectory = Path.Combine(coreDataPath, "placeable");
        if (Directory.Exists(placeableDirectory))
        {
            files.AddRange(Directory.GetFiles(
                placeableDirectory,
                "constructions*.json",
                SearchOption.TopDirectoryOnly));
        }

        files = files
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static file => file, StringComparer.Ordinal)
            .ToList();

        var definitionsById = new Dictionary<string, ConstructionDefinition>(StringComparer.OrdinalIgnoreCase);
        var errors = 0;
        var duplicatesSkipped = 0;
        foreach (var file in files)
        {
            try
            {
                foreach (var definition in ParseConstructionsFile(file))
                {
                    if (definition.PlaceableProfile.Footprint.W <= 0
                        || definition.PlaceableProfile.Footprint.D <= 0)
                    {
                        continue;
                    }

                    if (definitionsById.ContainsKey(definition.Id))
                    {
                        duplicatesSkipped++;
                        errors++;
                        messages.Add($"duplicate or case-ambiguous construction rejected: {definition.Id} from {Path.GetFileName(file)}");
                        continue;
                    }

                    definitionsById.Add(definition.Id, definition);
                }
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"error parsing {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        var catalog = ConstructionCatalogStore.Empty;
        try
        {
            catalog = ConstructionCatalogStore.FromDefinitions(definitionsById.Values
                .OrderBy(static definition => definition.Id, StringComparer.Ordinal));
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"load error: {ex.Message}");
        }

        return new ConstructionContentLoadResult(
            catalog,
            catalog.Count,
            errors,
            duplicatesSkipped,
            catalog.GetAllCategories().ToArray(),
            messages);
    }

    private static IEnumerable<ConstructionDefinition> ParseConstructionsFile(string file)
    {
        using var stream = File.OpenRead(file);
        using var document = JsonDocument.Parse(stream, JsonOptions);
        var root = document.RootElement;

        JsonElement constructionsArray;
        var isWorkshopFile = false;
        if (root.TryGetProperty("workshops", out constructionsArray)
            && constructionsArray.ValueKind == JsonValueKind.Array)
        {
            isWorkshopFile = true;
        }
        else if (!root.TryGetProperty("constructions", out constructionsArray)
                 || constructionsArray.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        WorkshopAttachment[]? attachments = null;
        if (isWorkshopFile
            && root.TryGetProperty("attachments", out var attachmentsArray)
            && attachmentsArray.ValueKind == JsonValueKind.Array)
        {
            attachments = ParseAttachments(attachmentsArray);
        }

        foreach (var element in constructionsArray.EnumerateArray())
        {
            var hasNestedProfile = element.TryGetProperty("placeable_profile", out var profileElement)
                                   && profileElement.ValueKind == JsonValueKind.Object;
            if (!hasNestedProfile)
                profileElement = element;

            if (!profileElement.TryGetProperty("footprint", out var footprintElement)
                || footprintElement.ValueKind != JsonValueKind.Object)
                continue;

            var definition = new ConstructionDefinition
            {
                Id = element.GetProperty("id").GetString() ?? string.Empty,
                Name = ParseOptionalString(element, "name") ?? string.Empty,
                Category = ParseOptionalString(element, "category") ?? string.Empty,
                BuildTimeTicks = element.TryGetProperty("build_time_ticks", out var buildTimeElement)
                    ? buildTimeElement.GetInt32()
                    : 1000,
                ResultMaterialId = ParseOptionalString(element, "result_material_id"),
                SkillRequired = ParseOptionalString(element, "skill_required")
                                ?? ParseOptionalString(element, "required_skill"),
                MaterialCosts = ParseMaterialCosts(element),
                PlaceableProfile = ParsePlaceableProfile(profileElement)
            };

            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                definition.Name = definition.Id;
            }

            definition.Io = ParseWorkshopIo(element);
            definition.AttachmentSlots = ParseStringArray(element, "attachment_slots");
            definition.PowerBaselineW = element.TryGetProperty("power_baseline_w", out var powerElement)
                ? powerElement.GetInt32()
                : null;
            definition.EraMin = ParseOptionalString(element, "era_min");
            definition.EraMax = ParseOptionalString(element, "era_max");

            if (attachments != null && attachments.Length > 0)
            {
                definition.Attachments = attachments;
            }

            definition.Validate();
            yield return definition;
        }
    }

    private static MaterialCost[] ParseMaterialCosts(JsonElement element)
    {
        var costs = new List<MaterialCost>();
        if (element.TryGetProperty("material_costs", out var materialsArray)
            && materialsArray.ValueKind == JsonValueKind.Array)
        {
            AddMaterialCosts(costs, materialsArray);
        }
        else if (element.TryGetProperty("materials_required", out materialsArray)
                 && materialsArray.ValueKind == JsonValueKind.Array)
        {
            AddMaterialCosts(costs, materialsArray);
        }

        return costs
            .OrderBy(static cost => cost.Tag ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static cost => cost.DefId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static cost => cost.Count)
            .ToArray();
    }

    private static void AddMaterialCosts(List<MaterialCost> into, JsonElement materialsArray)
    {
        foreach (var material in materialsArray.EnumerateArray())
        {
            into.Add(new MaterialCost
            {
                Tag = ParseOptionalString(material, "tag"),
                DefId = ParseOptionalString(material, "def_id") ?? ParseOptionalString(material, "defId"),
                Count = material.TryGetProperty("count", out var countElement) ? countElement.GetInt32() : 1
            });
        }
    }

    private static PlaceableProfile ParsePlaceableProfile(JsonElement profileElement)
    {
        var footprintElement = profileElement.GetProperty("footprint");
        var profile = new PlaceableProfile
        {
            Footprint = new Footprint(
                w: footprintElement.GetProperty("w").GetInt32(),
                d: footprintElement.GetProperty("d").GetInt32(),
                h: footprintElement.TryGetProperty("h", out var hElement) ? hElement.GetInt32() : 1),
            Passability = ParsePassabilityMode(profileElement),
            RequiresFloor = profileElement.TryGetProperty("requires_floor", out var requiresFloor)
                            && requiresFloor.GetBoolean(),
            ClearanceH = profileElement.TryGetProperty("clearance_h", out var clearance)
                ? clearance.GetInt32()
                : 0,
            BlocksLight = profileElement.TryGetProperty("blocks_light", out var blocksLight)
                          && blocksLight.GetBoolean(),
            Tags = ParseStringArray(profileElement, "tags") ?? Array.Empty<string>(),
            Effects = ParseEffects(profileElement)
        };

        return profile;
    }

    private static PassabilityMode ParsePassabilityMode(JsonElement profileElement)
    {
        if (!profileElement.TryGetProperty("passability", out var passabilityElement))
        {
            return PassabilityMode.Nonblocking;
        }

        var passability = passabilityElement.GetString() ?? string.Empty;
        if (passability.Equals("blocking", StringComparison.OrdinalIgnoreCase))
        {
            return PassabilityMode.Blocking;
        }

        if (passability.Equals("doorway", StringComparison.OrdinalIgnoreCase))
        {
            return PassabilityMode.Doorway;
        }

        return PassabilityMode.Nonblocking;
    }

    private static EffectsBlock ParseEffects(JsonElement profileElement)
    {
        var effects = new EffectsBlock();
        if (!profileElement.TryGetProperty("effects", out var effectsElement)
            || effectsElement.ValueKind != JsonValueKind.Object)
        {
            return effects;
        }

        effects.Beauty = effectsElement.TryGetProperty("beauty", out var beauty) ? beauty.GetInt32() : 0;
        effects.Comfort = effectsElement.TryGetProperty("comfort", out var comfort) ? comfort.GetInt32() : 0;
        effects.LightLumen = effectsElement.TryGetProperty("light_lumen", out var lumen) ? lumen.GetInt32() : 0;
        effects.HeatW = effectsElement.TryGetProperty("heat_w", out var heat) ? heat.GetInt32() : 0;
        return effects;
    }

    private static WorkshopIo? ParseWorkshopIo(JsonElement element)
    {
        if (!element.TryGetProperty("io", out var ioElement)
            || ioElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new WorkshopIo
        {
            InputSlots = ioElement.TryGetProperty("input_slots", out var inputSlots) ? inputSlots.GetInt32() : 4,
            OutputSlots = ioElement.TryGetProperty("output_slots", out var outputSlots) ? outputSlots.GetInt32() : 4,
            BufferSlots = ioElement.TryGetProperty("buffer_slots", out var bufferSlots) ? bufferSlots.GetInt32() : 2
        };
    }

    private static WorkshopAttachment[] ParseAttachments(JsonElement attachmentsArray)
    {
        var attachments = new List<WorkshopAttachment>();
        foreach (var element in attachmentsArray.EnumerateArray())
        {
            var attachment = new WorkshopAttachment
            {
                Id = ParseOptionalString(element, "id") ?? string.Empty,
                Name = ParseOptionalString(element, "name") ?? string.Empty,
                Slot = ParseOptionalString(element, "slot") ?? string.Empty,
                Era = ParseOptionalString(element, "era"),
                EraMin = ParseOptionalString(element, "era_min"),
                EraMax = ParseOptionalString(element, "era_max"),
                UpgradeTo = ParseOptionalString(element, "upgrade_to"),
                Tags = ParseStringArray(element, "tags"),
                PowerW = element.TryGetProperty("power_w", out var powerElement) ? powerElement.GetInt32() : 0
            };

            attachments.Add(attachment);
        }

        return attachments
            .OrderBy(static attachment => attachment.Id, StringComparer.Ordinal)
            .ToArray();
    }
}
