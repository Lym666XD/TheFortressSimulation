using System.Collections.Immutable;
using HumanFortress.Content.Definitions;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Stockpile;

namespace HumanFortress.Runtime.Content;

internal sealed class FortressRuntimeStockpilePresetCatalog
{
    internal static FortressRuntimeStockpilePresetCatalog Empty { get; } = new(new[] { RuntimeStockpilePreset.CreateAll() });

    private readonly Dictionary<string, RuntimeStockpilePreset> _presets;

    private FortressRuntimeStockpilePresetCatalog(IEnumerable<RuntimeStockpilePreset> presets)
    {
        _presets = presets
            .GroupBy(static preset => preset.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.Last(),
                StringComparer.OrdinalIgnoreCase);

        if (!_presets.ContainsKey("all"))
            _presets["all"] = RuntimeStockpilePreset.CreateAll();
    }

    internal RuntimeStockpilePreset Resolve(string? presetId)
    {
        var id = string.IsNullOrWhiteSpace(presetId) ? "all" : presetId;
        return _presets.TryGetValue(id, out var preset)
            ? preset
            : _presets["all"];
    }

    internal IReadOnlyList<RuntimeStockpilePreset> GetMenuPresets()
    {
        return _presets
            .OrderBy(static entry => string.Equals(entry.Key, "all", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(static entry => entry.Value.Priority)
            .ThenBy(static entry => entry.Value.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static entry => entry.Value)
            .ToArray();
    }

    internal static FortressRuntimeStockpilePresetCatalog Load(string baseDir, Action<string>? log = null)
    {
        return FromDefinitions(
            StockpilePresetLoader.Load(baseDir, log),
            "stockpile_presets.json",
            log);
    }

    internal static FortressRuntimeStockpilePresetCatalog FromDefinitions(
        IEnumerable<StockpilePresetDefinition> definitions,
        string source,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var presets = new List<RuntimeStockpilePreset>();

        foreach (var row in definitions)
        {
            if (string.IsNullOrWhiteSpace(row.Id))
            {
                log?.Invoke($"[STOCKPILE] Skipping stockpile preset with missing id in {source}.");
                continue;
            }

            if (!TryParseMode(row.Mode, out var mode))
            {
                log?.Invoke($"[STOCKPILE] Skipping stockpile preset '{row.Id}' with invalid mode '{row.Mode}' in {source}.");
                continue;
            }

            presets.Add(new RuntimeStockpilePreset(
                row.Id,
                string.IsNullOrWhiteSpace(row.Name) ? row.Id : row.Name,
                mode,
                ToIdSet(row.Tags),
                ToIdSet(row.ItemIds),
                ToIdSet(row.Materials),
                row.Priority));
        }

        return presets.Count == 0
            ? Empty
            : new FortressRuntimeStockpilePresetCatalog(presets);
    }

    private static bool TryParseMode(string? value, out FilterMode mode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            mode = FilterMode.Whitelist;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out mode);
    }

    private static ImmutableHashSet<string> ToIdSet(IEnumerable<string>? values)
    {
        return values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToImmutableHashSet(StringComparer.Ordinal)
            ?? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
    }
}

internal sealed record RuntimeStockpilePreset(
    string Id,
    string Name,
    FilterMode Mode,
    ImmutableHashSet<string> Tags,
    ImmutableHashSet<string> ItemIds,
    ImmutableHashSet<string> Materials,
    int Priority)
{
    internal StockpileFilter CreateFilter()
    {
        return new StockpileFilter
        {
            Mode = Mode,
            Tags = Tags,
            ItemIds = ItemIds,
            Materials = Materials
        };
    }

    internal static RuntimeStockpilePreset CreateAll()
    {
        return new RuntimeStockpilePreset(
            "all",
            "All",
            FilterMode.Whitelist,
            ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal),
            ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal),
            ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal),
            1);
    }
}
