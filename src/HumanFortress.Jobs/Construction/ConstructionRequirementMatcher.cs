namespace HumanFortress.Jobs.Construction;

internal static class ConstructionRequirementMatcher
{
    public static bool Matches(IEnumerable<string> itemTags, string requirement)
    {
        var set = new HashSet<string>(itemTags, StringComparer.OrdinalIgnoreCase);
        switch (requirement.ToLowerInvariant())
        {
            case "block":
                return set.Contains("block") || set.Contains("stone_block") || set.Contains("brick") || (set.Contains("stone") && set.Contains("block"));
            case "plank":
                return set.Contains("plank") || set.Contains("wood_plank") || (set.Contains("wood") && set.Contains("plank"));
            case "stone_block":
                return set.Contains("stone") && set.Contains("block");
            case "wood_plank":
                return set.Contains("wood") && set.Contains("plank");
            case "wood_log":
                return set.Contains("wood") && set.Contains("log");
            default:
                return set.Contains(requirement);
        }
    }
}
