using System;
using System.Collections.Generic;
using HumanFortress.Contracts.Simulation.Items;

namespace HumanFortress.Simulation.Placeables;

internal static class ConstructionMaterialRequirement
{
    internal const string DefinitionPrefix = "def:";
    internal const string TagPrefix = "tag:";

    internal static string ForTag(string tag)
    {
        return Normalize(tag);
    }

    internal static string ForDefinition(string definitionId)
    {
        return DefinitionPrefix + Normalize(definitionId);
    }

    internal static bool MatchesItem(ItemDefinition definition, string requirement)
    {
        return MatchesItem(definition.Id, definition.Tags ?? Array.Empty<string>(), requirement);
    }

    internal static bool MatchesItem(string itemDefinitionId, IEnumerable<string> itemTags, string requirement)
    {
        var normalizedRequirement = Normalize(requirement);
        if (TryGetDefinitionRequirement(normalizedRequirement, out var requiredDefinitionId))
            return string.Equals(itemDefinitionId, requiredDefinitionId, StringComparison.Ordinal);

        var tagRequirement = GetTagRequirement(normalizedRequirement);
        var set = new HashSet<string>(itemTags, StringComparer.OrdinalIgnoreCase);
        return tagRequirement.ToLowerInvariant() switch
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
            _ => set.Contains(tagRequirement)
        };
    }

    private static bool TryGetDefinitionRequirement(string requirement, out string definitionId)
    {
        if (requirement.StartsWith(DefinitionPrefix, StringComparison.Ordinal))
        {
            definitionId = requirement[DefinitionPrefix.Length..];
            return definitionId.Length > 0;
        }

        definitionId = string.Empty;
        return false;
    }

    private static string GetTagRequirement(string requirement)
    {
        return requirement.StartsWith(TagPrefix, StringComparison.Ordinal)
            ? requirement[TagPrefix.Length..]
            : requirement;
    }

    private static string Normalize(string value)
    {
        return value.Trim();
    }
}
