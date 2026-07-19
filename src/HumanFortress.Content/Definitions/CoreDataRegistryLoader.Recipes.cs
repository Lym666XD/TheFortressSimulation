using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HumanFortress.Content.Identity;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Definitions;

internal static partial class CoreDataRegistryLoader
{
    private static RecipeContentLoadResult LoadRecipeDefinitions(string recipesDir)
    {
        var messages = new List<string>();
        if (!Directory.Exists(recipesDir))
        {
            messages.Add($"directory not found: {recipesDir}");
            return new RecipeContentLoadResult(RecipeCatalogStore.Empty, 0, 0, messages);
        }

        var candidates = Directory.GetFiles(recipesDir, "*.json", SearchOption.TopDirectoryOnly)
            .Select(file => new
            {
                File = file,
                SourceId = "data/core/recipes/" + Path.GetFileName(file)
            })
            .OrderBy(static candidate => candidate.SourceId, StringComparer.Ordinal)
            .ToArray();
        var availableSourceIds = candidates
            .Select(static candidate => candidate.SourceId)
            .ToHashSet(StringComparer.Ordinal);
        var files = candidates
            .Where(candidate =>
                MechanicalContentSourceFamilyManifest.TryResolve(
                    candidate.SourceId,
                    availableSourceIds,
                    out var resolution)
                && resolution.IsActive
                && resolution.FamilyId.Equals("core.recipe", StringComparison.Ordinal))
            .Select(static candidate => candidate.File)
            .ToArray();

        var definitions = new List<RecipeDefinition>();
        var errors = 0;
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file), JsonOptions);
                JsonElement recipesArray;
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    recipesArray = document.RootElement;
                }
                else if (!document.RootElement.TryGetProperty("recipes", out recipesArray)
                         || recipesArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var element in recipesArray.EnumerateArray())
                {
                    var definition = ParseRecipeDefinition(element);
                    if (definition != null)
                    {
                        if (!seenIds.Add(definition.Id))
                        {
                            errors++;
                            messages.Add($"duplicate or case-ambiguous recipe rejected: {definition.Id} from {Path.GetFileName(file)}");
                            continue;
                        }
                        definitions.Add(definition);
                    }
                }
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"error parsing {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        var catalog = RecipeCatalogStore.FromDefinitions(definitions);
        return new RecipeContentLoadResult(catalog, catalog.Count, errors, messages);
    }

    private static RecipeDefinition? ParseRecipeDefinition(JsonElement element)
    {
        var id = element.TryGetProperty("id", out var idElement)
            ? idElement.GetString() ?? string.Empty
            : string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var name = element.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString() ?? id
            : id;

        var workshops = ParseRecipeWorkshops(element);
        if (workshops.Count == 0)
        {
            return null;
        }

        var outputs = ParseRecipeOutputs(element);
        if (outputs.Count == 0)
        {
            return null;
        }

        return new RecipeDefinition
        {
            Id = id,
            Name = name,
            Workshops = workshops
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray(),
            Inputs = ParseRecipeInputs(element)
                .OrderBy(static value => value.DefId, StringComparer.Ordinal)
                .ThenBy(static value => value.Count)
                .ToArray(),
            Outputs = outputs
                .OrderBy(static value => value.DefId, StringComparer.Ordinal)
                .ThenBy(static value => value.Count)
                .ToArray(),
            RequiredEnablers = ParseRequiredEnablers(element)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray(),
            DurationTicks = Math.Max(1, ParseRecipeDurationTicks(element)),
            PrimarySkill = ParsePrimarySkill(element),
            Era = ParseOptionalString(element, "era")
        };
    }

    private static List<string> ParseRecipeWorkshops(JsonElement element)
    {
        var workshops = new List<string>();
        if (element.TryGetProperty("workshops", out var workshopsArray)
            && workshopsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var workshop in workshopsArray.EnumerateArray())
            {
                AddIfNotBlank(workshops, workshop.GetString());
            }
        }
        else if (element.TryGetProperty("workshop_id", out var singleWorkshop)
                 && singleWorkshop.ValueKind == JsonValueKind.String)
        {
            AddIfNotBlank(workshops, singleWorkshop.GetString());
        }
        else if (element.TryGetProperty("workshop", out var legacyWorkshop)
                 && legacyWorkshop.ValueKind == JsonValueKind.String)
        {
            AddIfNotBlank(workshops, legacyWorkshop.GetString());
        }

        return workshops;
    }

    private static int ParseRecipeDurationTicks(JsonElement element)
    {
        if (element.TryGetProperty("work_time", out var workObject)
            && workObject.ValueKind == JsonValueKind.Object
            && workObject.TryGetProperty("duration_ticks", out var durationElement))
        {
            return durationElement.GetInt32();
        }

        if (element.TryGetProperty("duration_ticks", out var simpleDuration)
            && simpleDuration.ValueKind == JsonValueKind.Number)
        {
            return simpleDuration.GetInt32();
        }

        return 600;
    }

    private static string ParsePrimarySkill(JsonElement element)
    {
        var skill = "craft";
        if (element.TryGetProperty("skill", out var skillObject)
            && skillObject.ValueKind == JsonValueKind.Object
            && skillObject.TryGetProperty("primary", out var primary)
            && primary.ValueKind == JsonValueKind.String)
        {
            skill = primary.GetString() ?? skill;
        }
        else if (element.TryGetProperty("primary_skill", out var primarySkill)
                 && primarySkill.ValueKind == JsonValueKind.String)
        {
            skill = primarySkill.GetString() ?? skill;
        }

        return string.IsNullOrWhiteSpace(skill) ? "craft" : skill;
    }

    private static List<RecipeIngredient> ParseRecipeInputs(JsonElement element)
    {
        var inputs = new List<RecipeIngredient>();
        if (!element.TryGetProperty("inputs", out var inputsArray)
            || inputsArray.ValueKind != JsonValueKind.Array)
        {
            return inputs;
        }

        foreach (var input in inputsArray.EnumerateArray())
        {
            var definitionId = input.TryGetProperty("def_id", out var definitionElement)
                ? definitionElement.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                continue;
            }

            var count = input.TryGetProperty("count", out var countElement) ? countElement.GetInt32() : 1;
            inputs.Add(new RecipeIngredient { DefId = definitionId, Count = Math.Max(1, count) });
        }

        return inputs;
    }

    private static List<RecipeOutput> ParseRecipeOutputs(JsonElement element)
    {
        var outputs = new List<RecipeOutput>();
        if (!element.TryGetProperty("outputs", out var outputsArray)
            || outputsArray.ValueKind != JsonValueKind.Array)
        {
            return outputs;
        }

        foreach (var output in outputsArray.EnumerateArray())
        {
            var definitionId = output.TryGetProperty("def_id", out var definitionElement)
                ? definitionElement.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                continue;
            }

            var count = output.TryGetProperty("count", out var countElement) ? countElement.GetInt32() : 1;
            outputs.Add(new RecipeOutput { DefId = definitionId, Count = Math.Max(1, count) });
        }

        return outputs;
    }

    private static List<string> ParseRequiredEnablers(JsonElement element)
    {
        var enablers = new List<string>();
        if (element.TryGetProperty("requires_enablers", out var requiredArray)
            && requiredArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var required in requiredArray.EnumerateArray())
            {
                AddIfNotBlank(enablers, required.GetString());
            }
        }
        else if (element.TryGetProperty("requirements", out var requirementsObject)
                 && requirementsObject.ValueKind == JsonValueKind.Object
                 && requirementsObject.TryGetProperty("enablers", out var enablersArray)
                 && enablersArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var required in enablersArray.EnumerateArray())
            {
                AddIfNotBlank(enablers, required.GetString());
            }
        }

        return enablers;
    }
}
