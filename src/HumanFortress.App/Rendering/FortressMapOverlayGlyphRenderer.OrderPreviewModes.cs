using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using UiMiningAction = HumanFortress.App.UI.MiningAction;

namespace HumanFortress.App.Rendering;

internal static partial class FortressMapOverlayGlyphRenderer
{
    private static SimulationPlacementPreviewMode? ToHighlightMiningPreviewMode(string action, int currentZ, int zMin)
    {
        return action switch
        {
            nameof(UiMiningAction.DigRamp) => SimulationPlacementPreviewMode.MiningRamp,
            nameof(UiMiningAction.DigChannel) => SimulationPlacementPreviewMode.MiningChannel,
            nameof(UiMiningAction.DigStairwell) when currentZ == zMin => SimulationPlacementPreviewMode.MiningStairwellTop,
            nameof(UiMiningAction.DigStairwell) => null,
            _ => SimulationPlacementPreviewMode.MiningDig,
        };
    }

    private static SimulationPlacementPreviewMode? ToHighlightConstructionPreviewMode(string shape)
    {
        return shape switch
        {
            nameof(UiConstructionShape.Wall) => SimulationPlacementPreviewMode.ConstructionWall,
            nameof(UiConstructionShape.Floor) => SimulationPlacementPreviewMode.ConstructionFloor,
            nameof(UiConstructionShape.Ramp) => SimulationPlacementPreviewMode.ConstructionRamp,
            _ => null,
        };
    }
}
