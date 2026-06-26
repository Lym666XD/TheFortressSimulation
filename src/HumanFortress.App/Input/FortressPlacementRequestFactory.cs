using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime;
using UiMiningAction = HumanFortress.App.UI.MiningAction;

namespace HumanFortress.App.Input;

internal static class FortressPlacementRequestFactory
{
    public static RuntimeMiningAction ToRuntimeMiningAction(UiMiningAction action)
    {
        return action switch
        {
            UiMiningAction.Dig => RuntimeMiningAction.Dig,
            UiMiningAction.DigStairwell => RuntimeMiningAction.DigStairwell,
            UiMiningAction.DigRamp => RuntimeMiningAction.DigRamp,
            UiMiningAction.DigChannel => RuntimeMiningAction.DigChannel,
            UiMiningAction.RemoveDigging => RuntimeMiningAction.RemoveDigging,
            _ => RuntimeMiningAction.Dig
        };
    }

    public static RuntimeConstructionShape ToRuntimeConstructionShape(UiConstructionShape shape)
    {
        return shape switch
        {
            UiConstructionShape.Wall => RuntimeConstructionShape.Wall,
            UiConstructionShape.Floor => RuntimeConstructionShape.Floor,
            UiConstructionShape.Ramp => RuntimeConstructionShape.Ramp,
            UiConstructionShape.Stairs => RuntimeConstructionShape.Stairs,
            _ => RuntimeConstructionShape.Wall
        };
    }
}
