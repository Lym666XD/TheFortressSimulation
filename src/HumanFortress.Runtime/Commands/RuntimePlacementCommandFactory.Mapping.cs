using HumanFortress.Contracts.Runtime;
using HumanFortress.Simulation.Orders;

namespace HumanFortress.Runtime.Commands;

internal static partial class RuntimePlacementCommandFactory
{
    private static MiningAction ToSimulationMiningAction(RuntimeMiningAction action)
    {
        return action switch
        {
            RuntimeMiningAction.Dig => MiningAction.Dig,
            RuntimeMiningAction.DigStairwell => MiningAction.DigStairwell,
            RuntimeMiningAction.DigRamp => MiningAction.DigRamp,
            RuntimeMiningAction.DigChannel => MiningAction.DigChannel,
            RuntimeMiningAction.RemoveDigging => MiningAction.RemoveDigging,
            _ => MiningAction.Dig
        };
    }

    private static ConstructionShape ToSimulationConstructionShape(RuntimeConstructionShape shape)
    {
        return shape switch
        {
            RuntimeConstructionShape.Wall => ConstructionShape.Wall,
            RuntimeConstructionShape.Floor => ConstructionShape.Floor,
            RuntimeConstructionShape.Ramp => ConstructionShape.Ramp,
            RuntimeConstructionShape.Stairs => ConstructionShape.Stairs,
            _ => ConstructionShape.Wall
        };
    }
}
