using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal static partial class RuntimePlacementCommandFactory
{
    internal static Func<ulong, ICommand> CreateHaulOrder(Rectangle rect, int z, int priority = 50)
        => tick => new CreateHaulOrderCommand(tick, rect, z, priority);

    internal static Func<ulong, ICommand> CreateAdvancedMiningOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeMiningAction action,
        int priority = 50)
        => tick => new CreateAdvancedMiningOrderCommand(tick, rect, zMin, zMax, ToSimulationMiningAction(action), priority);

    internal static Func<ulong, ICommand> CreateConstructionOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeConstructionShape shape,
        RuntimeMaterialFilterSpec filter,
        int priority = 50)
        => tick => new CreateConstructionOrderCommand(
            tick,
            rect,
            zMin,
            zMax,
            ToSimulationConstructionShape(shape),
            ToSimulationMaterialFilter(filter),
            priority);

    internal static Func<ulong, ICommand> CreateBuildableConstructionOrder(
        string constructionId,
        Point anchor,
        int z,
        int priority = 50)
        => tick => new CreateBuildableConstructionOrderCommand(tick, constructionId, anchor, z, priority);

    internal static Func<ulong, ICommand> CreateZone(string defId, Rectangle rect, int z)
        => tick => new CreateZoneCommand(tick, defId, $"{defId}_zone", rect, z);

    internal static Func<ulong, ICommand> DeleteZone(int zoneId)
        => tick => new DeleteZoneCommand(tick, zoneId);

    internal static Func<ulong, ICommand> CreateStockpile(Rectangle rect, int z, string presetId)
        => tick => new CreateStockpileCommand(tick, rect, z, presetId);

    internal static Func<ulong, ICommand> DeleteStockpile(int zoneId)
        => tick => new DeleteStockpileCommand(tick, zoneId);
}
