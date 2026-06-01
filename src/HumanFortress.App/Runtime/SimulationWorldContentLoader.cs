using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.World;
using DataContentRegistry = HumanFortress.Core.Content.ContentRegistry;
using ItemContentRegistry = HumanFortress.Core.Content.Registry.ContentRegistry;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Loads data-backed world content for an active fortress simulation session.
/// </summary>
internal static class SimulationWorldContentLoader
{
    public static void LoadCoreContent(World world, string baseDir)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var dataPath = TryFindCoreDataPath(baseDir, out var publishedPath, out var developmentPath);
        if (dataPath == null)
        {
            Logger.Log("[GameStateManager] WARNING: Data directory not found. Tried:");
            Logger.Log($"  - {publishedPath}");
            Logger.Log($"  - {developmentPath}");
            return;
        }

        Logger.Log($"[GameStateManager] Loading creature and item definitions from {dataPath}");
        world.Creatures.LoadDefinitions(dataPath);

        world.Items.SetDependencies(world, ItemContentRegistry.Instance);
        world.Items.LoadDefinitions(dataPath);

        foreach (var zoneData in DataContentRegistry.Instance.Zones.Values)
        {
            world.Zones.Manager.RegisterDefinition(zoneData);
        }

        Logger.Log($"[GameStateManager] Loaded {world.Creatures.DefinitionCount} creatures, {world.Items.DefinitionCount} items, {world.Zones.Manager.GetAllDefinitions().Count()} zone definitions");

        try
        {
            LoadBuildableConstructions(Path.Combine(dataPath, "workshops"));
        }
        catch (Exception ex)
        {
            Logger.Log($"[CONSTR.REG] ERROR: failed loading constructions: {ex.Message}");
        }

        try
        {
            LoadRecipeDefinitions(Path.Combine(dataPath, "recipes"));
        }
        catch (Exception ex)
        {
            Logger.Log($"[RECIPES] ERROR: failed loading recipes: {ex.Message}");
        }
    }

    private static string? TryFindCoreDataPath(string baseDir, out string publishedPath, out string developmentPath)
    {
        publishedPath = Path.Combine(baseDir, "data", "core");
        if (Directory.Exists(publishedPath))
        {
            developmentPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "data", "core"));
            return publishedPath;
        }

        developmentPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "data", "core"));
        return Directory.Exists(developmentPath) ? developmentPath : null;
    }

    private static void LoadBuildableConstructions(string workshopsDir)
    {
        if (!Directory.Exists(workshopsDir))
        {
            Logger.Log($"[CONSTR.REG] workshops dir not found: {workshopsDir}");
            return;
        }

        var files = new List<string>();
        foreach (var file in Directory.GetFiles(workshopsDir, "core_workshop_*.json", SearchOption.TopDirectoryOnly))
        {
            files.Add(file);
        }

        var legacyPath = Path.Combine(Path.GetDirectoryName(workshopsDir) ?? "", "placeable", "workshops.json");
        if (File.Exists(legacyPath))
        {
            files.Add(legacyPath);
            Logger.Log("[CONSTR.REG] loading legacy workshops.json from placeable dir");
        }

        var defs = new List<ConstructionDefinition>();
        int errors = 0;
        foreach (var file in files)
        {
            try
            {
                foreach (var definition in ParseConstructionsFile(file))
                {
                    if (definition.PlaceableProfile != null
                        && definition.PlaceableProfile.Footprint.W > 0
                        && definition.PlaceableProfile.Footprint.D > 0)
                    {
                        defs.Add(definition);
                    }
                }
            }
            catch (Exception ex)
            {
                errors++;
                Logger.Log($"[CONSTR.REG] error parsing {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        var registry = ConstructionRegistry.Instance;
        registry.Clear();
        try
        {
            registry.LoadConstructions(defs);
        }
        catch (Exception ex)
        {
            errors++;
            Logger.Log($"[CONSTR.REG] load error: {ex.Message}");
        }

        var categories = string.Join(',', registry.GetAllCategories());
        Logger.Log($"[CONSTR.REG] loaded={registry.Count} categories=[{categories}] errors={errors}");
    }

    private static void LoadRecipeDefinitions(string recipesDir)
    {
        if (!Directory.Exists(recipesDir))
        {
            Logger.Log($"[RECIPES] directory not found: {recipesDir}");
            RecipeRegistry.Instance.Clear();
            return;
        }

        var files = Directory.GetFiles(recipesDir, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        var definitions = new List<RecipeDefinition>();
        int errors = 0;
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var options = new System.Text.Json.JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = System.Text.Json.JsonCommentHandling.Skip
                };
                using var document = System.Text.Json.JsonDocument.Parse(json, options);
                if (!document.RootElement.TryGetProperty("recipes", out var arr)
                    || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var element in arr.EnumerateArray())
                {
                    var definition = ParseRecipeDefinition(element);
                    if (definition != null)
                    {
                        definitions.Add(definition);
                    }
                }
            }
            catch (Exception ex)
            {
                errors++;
                Logger.Log($"[RECIPES] error parsing {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        RecipeRegistry.Instance.LoadRecipes(definitions);
        Logger.Log($"[RECIPES] loaded={RecipeRegistry.Instance.Count} errors={errors}");
    }

    private static RecipeDefinition? ParseRecipeDefinition(System.Text.Json.JsonElement element)
    {
        string id = element.TryGetProperty("id", out var idElement) ? (idElement.GetString() ?? string.Empty) : string.Empty;
        if (string.IsNullOrWhiteSpace(id)) return null;
        string name = element.TryGetProperty("name", out var nameElement) ? (nameElement.GetString() ?? id) : id;

        var workshops = new List<string>();
        if (element.TryGetProperty("workshops", out var workshopsArray)
            && workshopsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var workshop in workshopsArray.EnumerateArray())
            {
                var workshopId = workshop.GetString();
                if (!string.IsNullOrWhiteSpace(workshopId))
                    workshops.Add(workshopId);
            }
        }
        else if (element.TryGetProperty("workshop_id", out var singleWorkshop)
                 && singleWorkshop.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var workshopId = singleWorkshop.GetString();
            if (!string.IsNullOrWhiteSpace(workshopId))
                workshops.Add(workshopId);
        }
        else if (element.TryGetProperty("workshop", out var legacyWorkshop)
                 && legacyWorkshop.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var workshopId = legacyWorkshop.GetString();
            if (!string.IsNullOrWhiteSpace(workshopId))
                workshops.Add(workshopId);
        }

        if (workshops.Count == 0) return null;

        int duration = 600;
        if (element.TryGetProperty("work_time", out var workObject)
            && workObject.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            duration = workObject.TryGetProperty("duration_ticks", out var durationElement)
                ? durationElement.GetInt32()
                : duration;
        }
        else if (element.TryGetProperty("duration_ticks", out var simpleDuration)
                 && simpleDuration.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            duration = simpleDuration.GetInt32();
        }

        string skill = "craft";
        if (element.TryGetProperty("skill", out var skillObject)
            && skillObject.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (skillObject.TryGetProperty("primary", out var primary)
                && primary.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                skill = primary.GetString() ?? skill;
            }
        }
        else if (element.TryGetProperty("primary_skill", out var primarySkill)
                 && primarySkill.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            skill = primarySkill.GetString() ?? skill;
        }

        string? era = element.TryGetProperty("era", out var eraElement)
                      && eraElement.ValueKind == System.Text.Json.JsonValueKind.String
            ? eraElement.GetString()
            : null;

        var inputs = new List<RecipeIngredient>();
        if (element.TryGetProperty("inputs", out var inputsArray)
            && inputsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var input in inputsArray.EnumerateArray())
            {
                string definitionId = input.TryGetProperty("def_id", out var did)
                    ? (did.GetString() ?? string.Empty)
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(definitionId)) continue;
                int count = input.TryGetProperty("count", out var countElement) ? countElement.GetInt32() : 1;
                inputs.Add(new RecipeIngredient { DefId = definitionId, Count = Math.Max(1, count) });
            }
        }

        var outputs = new List<RecipeOutput>();
        if (element.TryGetProperty("outputs", out var outputsArray)
            && outputsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var output in outputsArray.EnumerateArray())
            {
                string definitionId = output.TryGetProperty("def_id", out var did)
                    ? (did.GetString() ?? string.Empty)
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(definitionId)) continue;
                int count = output.TryGetProperty("count", out var countElement) ? countElement.GetInt32() : 1;
                outputs.Add(new RecipeOutput { DefId = definitionId, Count = Math.Max(1, count) });
            }
        }

        if (outputs.Count == 0) return null;

        var enablers = new List<string>();
        if (element.TryGetProperty("requires_enablers", out var requiredArray)
            && requiredArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var required in requiredArray.EnumerateArray())
            {
                var value = required.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    enablers.Add(value);
            }
        }
        else if (element.TryGetProperty("requirements", out var requirementsObject)
                 && requirementsObject.ValueKind == System.Text.Json.JsonValueKind.Object
                 && requirementsObject.TryGetProperty("enablers", out var enablersArray)
                 && enablersArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var required in enablersArray.EnumerateArray())
            {
                var value = required.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    enablers.Add(value);
            }
        }

        return new RecipeDefinition
        {
            Id = id,
            Name = name,
            Workshops = workshops.ToArray(),
            Inputs = inputs.ToArray(),
            Outputs = outputs.ToArray(),
            RequiredEnablers = enablers.ToArray(),
            DurationTicks = Math.Max(1, duration),
            PrimarySkill = string.IsNullOrWhiteSpace(skill) ? "craft" : skill,
            Era = era
        };
    }

    private static IEnumerable<ConstructionDefinition> ParseConstructionsFile(string file)
    {
        using var stream = File.OpenRead(file);
        using var document = System.Text.Json.JsonDocument.Parse(stream);
        var root = document.RootElement;

        System.Text.Json.JsonElement constructionsArray;
        bool isWorkshopFile;
        if (root.TryGetProperty("workshops", out constructionsArray)
            && constructionsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            isWorkshopFile = true;
        }
        else if (root.TryGetProperty("constructions", out constructionsArray)
                 && constructionsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            isWorkshopFile = false;
        }
        else
        {
            yield break;
        }

        WorkshopAttachment[]? attachments = null;
        if (isWorkshopFile
            && root.TryGetProperty("attachments", out var attachmentsArray)
            && attachmentsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            attachments = ParseAttachments(attachmentsArray);
        }

        foreach (var element in constructionsArray.EnumerateArray())
        {
            var hasProfile = element.TryGetProperty("placeable_profile", out var profileElement)
                             && profileElement.ValueKind == System.Text.Json.JsonValueKind.Object;
            if (!hasProfile)
                continue;

            var definition = new ConstructionDefinition
            {
                Id = element.GetProperty("id").GetString() ?? string.Empty,
                Name = element.TryGetProperty("name", out var nameElement)
                    ? (nameElement.GetString() ?? string.Empty)
                    : string.Empty,
                Category = element.TryGetProperty("category", out var categoryElement)
                    ? (categoryElement.GetString() ?? "")
                    : "",
                BuildTimeTicks = element.TryGetProperty("build_time_ticks", out var buildTimeElement)
                    ? buildTimeElement.GetInt32()
                    : 1000
            };
            if (string.IsNullOrWhiteSpace(definition.Name))
                definition.Name = definition.Id;

            definition.MaterialCosts = ParseMaterialCosts(element);
            definition.PlaceableProfile = ParsePlaceableProfile(profileElement);

            if (element.TryGetProperty("io", out var ioElement)
                && ioElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                definition.Io = new WorkshopIo
                {
                    InputSlots = ioElement.TryGetProperty("input_slots", out var inputSlots) ? inputSlots.GetInt32() : 4,
                    OutputSlots = ioElement.TryGetProperty("output_slots", out var outputSlots) ? outputSlots.GetInt32() : 4,
                    BufferSlots = ioElement.TryGetProperty("buffer_slots", out var bufferSlots) ? bufferSlots.GetInt32() : 2
                };
            }

            if (element.TryGetProperty("attachment_slots", out var slotsElement)
                && slotsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                definition.AttachmentSlots = slotsElement.EnumerateArray()
                    .Select(slot => slot.GetString() ?? "")
                    .Where(slot => !string.IsNullOrWhiteSpace(slot))
                    .ToArray();
            }

            if (element.TryGetProperty("power_baseline_w", out var powerElement))
            {
                definition.PowerBaselineW = powerElement.GetInt32();
            }

            if (element.TryGetProperty("era_min", out var eraMinElement))
            {
                definition.EraMin = eraMinElement.GetString();
            }

            if (element.TryGetProperty("era_max", out var eraMaxElement))
            {
                definition.EraMax = eraMaxElement.GetString();
            }

            if (attachments != null && attachments.Length > 0)
            {
                definition.Attachments = attachments;
            }

            definition.Validate();
            yield return definition;
        }
    }

    private static MaterialCost[] ParseMaterialCosts(System.Text.Json.JsonElement element)
    {
        var costs = new List<MaterialCost>();
        System.Text.Json.JsonElement materialsArray;
        if (element.TryGetProperty("material_costs", out materialsArray)
            && materialsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            AddMaterialCosts(costs, materialsArray);
        }
        else if (element.TryGetProperty("materials_required", out materialsArray)
                 && materialsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            AddMaterialCosts(costs, materialsArray);
        }

        return costs.ToArray();
    }

    private static void AddMaterialCosts(List<MaterialCost> into, System.Text.Json.JsonElement materialsArray)
    {
        foreach (var material in materialsArray.EnumerateArray())
        {
            into.Add(new MaterialCost
            {
                Tag = material.TryGetProperty("tag", out var tagElement) ? tagElement.GetString() : null,
                DefId = material.TryGetProperty("def_id", out var defElement)
                    ? defElement.GetString()
                    : (material.TryGetProperty("defId", out var legacyDefElement) ? legacyDefElement.GetString() : null),
                Count = material.TryGetProperty("count", out var countElement) ? countElement.GetInt32() : 1
            });
        }
    }

    private static PlaceableProfile ParsePlaceableProfile(System.Text.Json.JsonElement profileElement)
    {
        var footprintElement = profileElement.GetProperty("footprint");
        var profile = new PlaceableProfile
        {
            Footprint = new Footprint(
                w: footprintElement.GetProperty("w").GetInt32(),
                d: footprintElement.GetProperty("d").GetInt32(),
                h: footprintElement.TryGetProperty("h", out var hElement) ? hElement.GetInt32() : 1)
        };

        if (profileElement.TryGetProperty("passability", out var passabilityElement))
        {
            var passability = (passabilityElement.GetString() ?? "nonblocking").Trim().ToLowerInvariant();
            profile.Passability = passability switch
            {
                "blocking" => PassabilityMode.Blocking,
                "doorway" => PassabilityMode.Doorway,
                _ => PassabilityMode.Nonblocking
            };
        }

        profile.RequiresFloor = profileElement.TryGetProperty("requires_floor", out var requiresFloor)
                                && requiresFloor.GetBoolean();
        profile.ClearanceH = profileElement.TryGetProperty("clearance_h", out var clearance)
            ? clearance.GetInt32()
            : 0;
        profile.BlocksLight = profileElement.TryGetProperty("blocks_light", out var blocksLight)
                              && blocksLight.GetBoolean();

        if (profileElement.TryGetProperty("tags", out var tagsElement)
            && tagsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            profile.Tags = tagsElement.EnumerateArray()
                .Select(tag => tag.GetString() ?? string.Empty)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToArray();
        }

        var effects = new EffectsBlock();
        if (profileElement.TryGetProperty("effects", out var effectsElement)
            && effectsElement.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            effects.Beauty = effectsElement.TryGetProperty("beauty", out var beauty) ? beauty.GetInt32() : 0;
            effects.Comfort = effectsElement.TryGetProperty("comfort", out var comfort) ? comfort.GetInt32() : 0;
            effects.LightLumen = effectsElement.TryGetProperty("light_lumen", out var lumen) ? lumen.GetInt32() : 0;
            effects.HeatW = effectsElement.TryGetProperty("heat_w", out var heat) ? heat.GetInt32() : 0;
        }
        profile.Effects = effects;

        return profile;
    }

    private static WorkshopAttachment[] ParseAttachments(System.Text.Json.JsonElement attachmentsArray)
    {
        var attachments = new List<WorkshopAttachment>();
        foreach (var element in attachmentsArray.EnumerateArray())
        {
            var attachment = new WorkshopAttachment
            {
                Id = element.TryGetProperty("id", out var idElement) ? (idElement.GetString() ?? "") : "",
                Name = element.TryGetProperty("name", out var nameElement) ? (nameElement.GetString() ?? "") : "",
                Slot = element.TryGetProperty("slot", out var slotElement) ? (slotElement.GetString() ?? "") : "",
                Era = element.TryGetProperty("era", out var eraElement) ? eraElement.GetString() : null,
                EraMin = element.TryGetProperty("era_min", out var eraMinElement) ? eraMinElement.GetString() : null,
                EraMax = element.TryGetProperty("era_max", out var eraMaxElement) ? eraMaxElement.GetString() : null,
                UpgradeTo = element.TryGetProperty("upgrade_to", out var upgradeElement) ? upgradeElement.GetString() : null,
                PowerW = element.TryGetProperty("power_w", out var powerElement) ? powerElement.GetInt32() : 0
            };

            if (element.TryGetProperty("tags", out var tagsElement)
                && tagsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                attachment.Tags = tagsElement.EnumerateArray()
                    .Select(tag => tag.GetString() ?? "")
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .ToArray();
            }

            attachments.Add(attachment);
        }

        return attachments.ToArray();
    }
}
