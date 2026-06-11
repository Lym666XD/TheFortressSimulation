using HumanFortress.App.Commands;
using HumanFortress.Core.Commands;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;
using UiMiningAction = HumanFortress.App.UI.MiningAction;

namespace HumanFortress.App.Runtime;

internal static class FortressPlacementCommandFactory
{
    public static Func<ulong, ICommand> CreateHaulOrder(Rectangle rect, int z, int priority = 50)
        => tick => new CreateHaulOrderCommand(tick, rect, z, priority);

    public static Func<ulong, ICommand> CreateAdvancedMiningOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        UiMiningAction action,
        int priority = 50)
        => tick => new CreateAdvancedMiningOrderCommand(tick, rect, zMin, zMax, action, priority);

    public static Func<ulong, ICommand> CreateConstructionOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        ConstructionShape shape,
        MaterialFilterSpec filter,
        int priority = 50)
        => tick => new CreateConstructionOrderCommand(tick, rect, zMin, zMax, shape, filter, priority);

    public static Func<ulong, ICommand> CreateBuildableConstructionOrder(
        string constructionId,
        Point anchor,
        int z,
        int priority = 50)
        => tick => new CreateBuildableConstructionOrderCommand(tick, constructionId, anchor, z, priority);

    public static Func<ulong, ICommand> CreateZone(string defId, Rectangle rect, int z)
        => tick => new CreateZoneCommand(tick, defId, $"{defId}_zone", rect, z);

    public static Func<ulong, ICommand> DeleteZone(int zoneId)
        => tick => new DeleteZoneCommand(tick, zoneId);

    public static Func<ulong, ICommand> CreateStockpile(Rectangle rect, int z, string presetId)
        => tick => new CreateStockpileCommand(tick, rect, z, presetId);

    public static MiningAction ToSimulationMiningAction(UiMiningAction action)
    {
        return action switch
        {
            UiMiningAction.Dig => MiningAction.Dig,
            UiMiningAction.DigStairwell => MiningAction.DigStairwell,
            UiMiningAction.DigRamp => MiningAction.DigRamp,
            UiMiningAction.DigChannel => MiningAction.DigChannel,
            UiMiningAction.RemoveDigging => MiningAction.RemoveDigging,
            _ => MiningAction.Dig
        };
    }

    public static MaterialFilterSpec CreateMaterialFilter(
        ConstructionShape shape,
        string? preferredMaterialId,
        string[] tags)
    {
        return new MaterialFilterSpec
        {
            PreferredMaterialId = preferredMaterialId ?? "core_mat_stone_granite",
            CategoryKey = shape switch
            {
                ConstructionShape.Wall => "l0.wall",
                ConstructionShape.Floor => "l0.floor",
                ConstructionShape.Ramp => "l0.ramp",
                ConstructionShape.Stairs => "l0.stairs",
                _ => "l0.unknown"
            },
            Tags = tags
        };
    }
}
