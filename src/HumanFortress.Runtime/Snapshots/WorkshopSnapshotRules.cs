using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Runtime.Snapshots;

internal static class WorkshopSnapshotRules
{
    internal static bool IsWorkshopDefinition(ConstructionDefinition definition)
    {
        return string.Equals(definition.Category, "workshop", StringComparison.OrdinalIgnoreCase)
            || (definition.PlaceableProfile.Tags != null
                && definition.PlaceableProfile.Tags.Any(tag => string.Equals(tag, "workshop", StringComparison.OrdinalIgnoreCase)));
    }

    internal static BuildableConstructionView ToBuildableConstructionView(ConstructionDefinition definition)
    {
        var footprint = definition.PlaceableProfile.Footprint;
        return new BuildableConstructionView(
            definition.Id,
            definition.Name,
            definition.Category,
            footprint.W,
            footprint.D,
            footprint.H,
            definition.PlaceableProfile.Passability.ToString(),
            definition.PlaceableProfile.Tags?.ToArray() ?? Array.Empty<string>());
    }

    internal static bool MaterialMatchesRequirement(IEnumerable<string> itemTags, string requirement)
    {
        var set = new HashSet<string>(itemTags, StringComparer.OrdinalIgnoreCase);
        return requirement.ToLowerInvariant() switch
        {
            "block" => set.Contains("block")
                || set.Contains("stone_block")
                || set.Contains("brick")
                || (set.Contains("stone") && set.Contains("block")),
            "plank" => set.Contains("plank")
                || set.Contains("wood_plank")
                || (set.Contains("wood") && set.Contains("plank")),
            "stone_block" => set.Contains("stone") && set.Contains("block"),
            "wood_plank" => set.Contains("wood") && set.Contains("plank"),
            "wood_log" => set.Contains("wood") && set.Contains("log"),
            _ => set.Contains(requirement)
        };
    }
}
