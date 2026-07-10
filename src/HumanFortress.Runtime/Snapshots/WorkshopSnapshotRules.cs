using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Simulation.Items;
using HumanFortress.Simulation.Placeables;

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

    internal static bool MaterialMatchesRequirement(ItemDefinition definition, string requirement)
    {
        return ConstructionMaterialRequirement.MatchesItem(definition, requirement);
    }
}
