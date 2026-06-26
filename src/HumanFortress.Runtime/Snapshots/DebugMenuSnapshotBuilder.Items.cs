using HumanFortress.Contracts.Simulation.Items;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class DebugMenuSnapshotBuilder
{
    private static IReadOnlyList<DebugItemCategoryView> CreateItemCategories(IReadOnlyList<ItemDefinition> definitions)
    {
        return new[]
        {
            CreateItemCategory(definitions, "Boulders", definition => HasIdPrefix(definition, "core_item_boulder_")),
            CreateItemCategory(definitions, "Blocks", definition => HasIdPrefix(definition, "core_item_block_")),
            CreateItemCategory(definitions, "Logs", definition => HasIdPrefix(definition, "core_item_log_")),
            CreateItemCategory(definitions, "Planks", definition => HasIdPrefix(definition, "core_item_plank_")),
            CreateItemCategory(definitions, "Tools", definition => HasIdPrefix(definition, "core_tool_")),
            CreateItemCategory(definitions, "Weapons", definition => HasKind(definition, "weapon")),
            CreateItemCategory(definitions, "Ammo", definition => HasKind(definition, "ammo")),
            CreateItemCategory(definitions, "SiegeWeapons", definition => HasKind(definition, "siege_weapon"))
        };
    }

    private static DebugItemCategoryView CreateItemCategory(
        IEnumerable<ItemDefinition> definitions,
        string categoryId,
        Func<ItemDefinition, bool> predicate)
    {
        return new DebugItemCategoryView(
            categoryId,
            definitions
                .Where(predicate)
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .Select(definition => new DebugItemView(definition.Id, FormatItemName(definition)))
                .ToList());
    }

    private static bool HasIdPrefix(ItemDefinition definition, string prefix)
    {
        return definition.Id.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static bool HasKind(ItemDefinition definition, string kind)
    {
        return string.Equals(definition.Kind, kind, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatItemName(ItemDefinition definition)
    {
        var baseName = string.IsNullOrWhiteSpace(definition.Name)
            ? definition.Id
            : definition.Name!;
        if (!string.IsNullOrEmpty(definition.FixedMaterial) && IsGenericResourceName(baseName))
        {
            var material = MaterialSuffixFriendly(definition.FixedMaterial!);
            if (!string.IsNullOrEmpty(material))
                return $"{material} {baseName}";
        }

        return baseName;
    }

    private static bool IsGenericResourceName(string name)
    {
        var normalized = name.ToLowerInvariant();
        return normalized == "boulder"
            || normalized == "block"
            || normalized == "plank"
            || normalized == "log";
    }

    private static string MaterialSuffixFriendly(string materialId)
    {
        try
        {
            var parts = materialId.Split('_');
            var last = parts[^1];
            return last.Length == 0
                ? materialId
                : char.ToUpperInvariant(last[0]) + last[1..].Replace('_', ' ');
        }
        catch
        {
            return materialId;
        }
    }
}
