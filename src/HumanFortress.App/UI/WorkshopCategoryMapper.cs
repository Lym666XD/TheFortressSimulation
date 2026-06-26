using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.UI;

/// <summary>
/// Loads workshop category -> tags mapping from content/registries/ui.workshop_categories.json.
/// Provides helpers to get tags or filter constructions by category.
/// Falls back to a baked-in mapping if file is missing.
/// </summary>
internal static partial class WorkshopCategoryMapper
{
    private static readonly object _lock = new();
    private static bool _loaded = false;
    private static Dictionary<string, string[]> _map = new(StringComparer.OrdinalIgnoreCase);

    public static string[] GetTagsForCategory(string category)
    {
        EnsureLoaded();
        if (_map.TryGetValue(category, out var tags)) return tags;
        return Array.Empty<string>();
    }

    public static List<BuildableConstructionView> GetWorkshopsByCategory(SimulationBuildCatalogData buildCatalog, string category)
    {
        EnsureLoaded();
        var tags = new HashSet<string>(GetTagsForCategory(category), StringComparer.OrdinalIgnoreCase);
        var list = new List<BuildableConstructionView>();
        foreach (var definition in buildCatalog.Workshops ?? Array.Empty<BuildableConstructionView>())
        {
            if (definition.Tags == null || definition.Tags.Count == 0) continue;
            foreach (var tag in tags)
            {
                if (HasTag(definition, tag)) { list.Add(definition); break; }
            }
        }
        return list;
    }

    private static bool HasTag(BuildableConstructionView definition, string tag)
    {
        return definition.Tags.Any(candidate => string.Equals(candidate, tag, StringComparison.Ordinal));
    }
}
