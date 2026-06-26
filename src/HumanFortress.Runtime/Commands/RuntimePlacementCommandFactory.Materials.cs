using HumanFortress.Contracts.Runtime;
using HumanFortress.Simulation.Orders;

namespace HumanFortress.Runtime.Commands;

internal static partial class RuntimePlacementCommandFactory
{
    internal static RuntimeMaterialFilterSpec CreateMaterialFilter(
        RuntimeConstructionShape shape,
        string? preferredMaterialId,
        string[] tags)
    {
        return new RuntimeMaterialFilterSpec(
            preferredMaterialId ?? "core_mat_stone_granite",
            shape switch
            {
                RuntimeConstructionShape.Wall => "l0.wall",
                RuntimeConstructionShape.Floor => "l0.floor",
                RuntimeConstructionShape.Ramp => "l0.ramp",
                RuntimeConstructionShape.Stairs => "l0.stairs",
                _ => "l0.unknown"
            },
            tags);
    }

    private static MaterialFilterSpec ToSimulationMaterialFilter(RuntimeMaterialFilterSpec filter)
    {
        return new MaterialFilterSpec
        {
            PreferredMaterialId = filter.PreferredMaterialId,
            CategoryKey = filter.CategoryKey,
            Tags = filter.Tags
        };
    }
}
