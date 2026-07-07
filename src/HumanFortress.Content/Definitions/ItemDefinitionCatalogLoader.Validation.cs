using HumanFortress.Contracts.Simulation.Items;

namespace HumanFortress.Content.Definitions;

internal static partial class ItemDefinitionCatalogLoader
{
    private static void NormalizeDefinition(ItemDefinition def)
    {
        var validKinds = new[] { "resource", "weapon", "armor", "tool", "container", "consumable", "placeable" };
        if (!validKinds.Contains(def.Kind.ToLowerInvariant())
            && def.Tags.Any(tag => string.Equals(tag, "furniture", StringComparison.OrdinalIgnoreCase)))
        {
            def.Kind = "placeable";
        }
    }

    private static void EnrichGenericResourceName(ItemDefinition def, List<string> messages)
    {
        if (string.IsNullOrWhiteSpace(def.FixedMaterial) || !IsGenericResourceName(def.Name))
        {
            return;
        }

        var oldName = def.Name;
        var materialName = MaterialSuffixFriendly(def.FixedMaterial!);
        if (string.IsNullOrEmpty(materialName))
        {
            return;
        }

        def.Name = $"{materialName} {def.Name}";
        if (def.Id.Contains("boulder", StringComparison.Ordinal))
        {
            messages.Add($"[ItemManager] Boulder name enriched: id={def.Id} '{oldName}' -> '{def.Name}' (mat={def.FixedMaterial})");
        }
    }

    private static void ValidateDefinition(ItemDefinition def, List<string> messages)
    {
        if (string.IsNullOrWhiteSpace(def.Id))
        {
            throw new ArgumentException("Item ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(def.Name))
        {
            throw new ArgumentException($"Item '{def.Id}' has no name");
        }

        var validKinds = new[] { "resource", "weapon", "armor", "tool", "container", "consumable", "placeable", "ammo", "siege_weapon" };
        if (!validKinds.Contains(def.Kind.ToLowerInvariant()))
        {
            throw new ArgumentException($"Item '{def.Id}' has invalid kind: {def.Kind}");
        }

        if (!string.IsNullOrWhiteSpace(def.FixedMaterial)
            && !def.FixedMaterial.StartsWith("core_mat_", StringComparison.Ordinal))
        {
            messages.Add($"[ItemManager] WARNING: Item '{def.Id}' has unusual material: {def.FixedMaterial}");
        }
    }

    private static bool IsGenericResourceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = name.Trim().ToLowerInvariant();
        return normalized == "boulder" || normalized == "block" || normalized == "plank" || normalized == "log";
    }

    private static string MaterialSuffixFriendly(string materialId)
    {
        try
        {
            var parts = materialId.Split('_');
            if (parts.Length >= 1)
            {
                var last = parts[^1];
                if (!string.IsNullOrEmpty(last))
                {
                    return char.ToUpperInvariant(last[0]) + last[1..].Replace('_', ' ');
                }
            }
        }
        catch
        {
        }

        return materialId;
    }
}
