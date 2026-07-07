using System.Text.Json;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Content.Loading;

namespace HumanFortress.Content.Definitions;

internal static class StockpilePresetLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    internal static IReadOnlyList<StockpilePresetDefinition> Load(string baseDir, Action<string>? log = null)
    {
        var file = FortressContentLoader.ResolveRegistryFile(baseDir, "stockpile_presets.json");
        if (!file.Found)
        {
            log?.Invoke($"[STOCKPILE] stockpile_presets.json not found. Tried {file.PublishedPath} and {file.DevelopmentPath}; using all-items preset.");
            return Array.Empty<StockpilePresetDefinition>();
        }

        try
        {
            var json = File.ReadAllText(file.ResolvedPath!);
            return LoadJson(json, file.ResolvedPath!, log);
        }
        catch (Exception ex)
        {
            log?.Invoke($"[STOCKPILE] Failed to load stockpile presets from {file.ResolvedPath}: {ex.Message}; using all-items preset.");
            return Array.Empty<StockpilePresetDefinition>();
        }
    }

    internal static IReadOnlyList<StockpilePresetDefinition> LoadJson(
        string json,
        string source,
        Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<StockpilePresetDefinition>();

        var rows = JsonSerializer.Deserialize<List<StockpilePresetDefinition>>(json, JsonOptions)
            ?? new List<StockpilePresetDefinition>();
        var result = new List<StockpilePresetDefinition>(rows.Count);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Id))
            {
                log?.Invoke($"[STOCKPILE] Skipping stockpile preset with missing id in {source}.");
                continue;
            }

            result.Add(row);
        }

        return result;
    }
}
