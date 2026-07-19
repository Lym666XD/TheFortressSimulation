using HumanFortress.Contracts.Simulation.Items;
using HumanFortress.Simulation.Placeables;

namespace HumanFortress.Jobs.Construction;

internal static class ConstructionRequirementMatcher
{
    internal static bool Matches(ItemDefinition definition, string requirement)
    {
        return ConstructionMaterialRequirement.MatchesItem(definition, requirement);
    }
}
