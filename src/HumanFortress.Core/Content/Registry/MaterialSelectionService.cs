using System.Collections.Concurrent;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Reusable selector/cache for user-chosen build materials.
/// Stores last-used material ID per category key (e.g., "l0.floor", "l2.bed").
/// Does not fetch items from world; only resolves preference and provides a fast cache.
/// </summary>
public static class MaterialSelectionService
{
    private static readonly ConcurrentDictionary<string, string> _lastUsedByCategory = new();

    public static void SetLastUsed(string categoryKey, string materialId)
    {
        if (string.IsNullOrWhiteSpace(categoryKey) || string.IsNullOrWhiteSpace(materialId)) return;
        _lastUsedByCategory[categoryKey] = materialId;
    }

    public static string? GetLastUsed(string categoryKey)
    {
        return _lastUsedByCategory.TryGetValue(categoryKey, out var v) ? v : null;
    }
}

