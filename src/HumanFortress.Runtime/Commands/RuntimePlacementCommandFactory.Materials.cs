using HumanFortress.Contracts.Runtime;
using HumanFortress.Simulation.Orders;

namespace HumanFortress.Runtime.Commands;

internal static partial class RuntimePlacementCommandFactory
{
    internal static RuntimeMaterialFilterSpec CreateMaterialFilter(
        RuntimeConstructionShape shape,
        string? preferredMaterialId,
        RuntimeConstructionMaterialRequirement[] requirements)
    {
        return new RuntimeMaterialFilterSpec(
            preferredMaterialId,
            shape switch
            {
                RuntimeConstructionShape.Wall => "l0.wall",
                RuntimeConstructionShape.Floor => "l0.floor",
                RuntimeConstructionShape.Ramp => "l0.ramp",
                RuntimeConstructionShape.Stairs => "l0.stairs",
                _ => "l0.unknown"
            },
            Array.Empty<string>(),
            requirements ?? Array.Empty<RuntimeConstructionMaterialRequirement>());
    }

    private static MaterialFilterSpec ToSimulationMaterialFilter(RuntimeMaterialFilterSpec filter)
    {
        return new MaterialFilterSpec
        {
            PreferredMaterialId = filter.PreferredMaterialId,
            CategoryKey = filter.CategoryKey,
            Tags = filter.Tags,
            Requirements = filter.Requirements
                .Where(static requirement =>
                    !string.IsNullOrWhiteSpace(requirement.Tag)
                    || !string.IsNullOrWhiteSpace(requirement.DefinitionId))
                .Select(static requirement => new MaterialRequirementSpec(
                    requirement.Tag,
                    requirement.DefinitionId,
                    Math.Max(1, requirement.Count)))
                .OrderBy(static requirement => requirement.Tag ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static requirement => requirement.DefinitionId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static requirement => requirement.Count)
                .ToArray()
        };
    }
}
