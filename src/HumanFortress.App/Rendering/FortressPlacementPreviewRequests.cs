using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;
using UiMiningAction = HumanFortress.App.UI.MiningAction;

namespace HumanFortress.App.Rendering;

internal static class FortressPlacementPreviewRequests
{
    internal static SimulationPlacementPreviewRequestData[] Build(
        UiStore ui,
        RuntimeViewportGeometry viewport,
        Point mouseWorld,
        bool includeActivePlacement)
    {
        var requests = new List<SimulationPlacementPreviewRequestData>();
        if (includeActivePlacement && ui.PlaceFirstCorner is Point firstCorner)
        {
            var activeMode = ActivePlacementMode(ui);
            if (activeMode.HasValue)
            {
                requests.Add(Create(
                    firstCorner,
                    mouseWorld,
                    viewport.CurrentZ,
                    activeMode.Value));
            }
        }

        foreach (var highlight in ui.GetHighlights())
        {
            if (viewport.CurrentZ < highlight.ZMin
                || viewport.CurrentZ > highlight.ZMax)
            {
                continue;
            }

            var mode = HighlightMode(highlight, viewport.CurrentZ);
            if (!mode.HasValue)
                continue;

            requests.Add(Create(
                new Point(highlight.Rect.X, highlight.Rect.Y),
                new Point(
                    highlight.Rect.X + highlight.Rect.Width - 1,
                    highlight.Rect.Y + highlight.Rect.Height - 1),
                viewport.CurrentZ,
                mode.Value));
        }

        return SimulationPlacementPreviewRequestData.CanonicalizeAll(requests);
    }

    internal static SimulationPlacementPreviewData Find(
        SimulationPlacementPreviewFrameData frame,
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode)
    {
        return frame.Find(Create(first, second, z, mode));
    }

    internal static SimulationPlacementPreviewMode ForMiningAction(MiningAction action)
    {
        return action switch
        {
            UiMiningAction.DigRamp => SimulationPlacementPreviewMode.MiningRamp,
            UiMiningAction.DigChannel => SimulationPlacementPreviewMode.MiningChannel,
            UiMiningAction.DigStairwell => SimulationPlacementPreviewMode.MiningStairwell,
            _ => SimulationPlacementPreviewMode.MiningDig,
        };
    }

    internal static SimulationPlacementPreviewMode ForConstructionShape(
        UiConstructionShape shape)
    {
        return shape switch
        {
            UiConstructionShape.Floor => SimulationPlacementPreviewMode.ConstructionFloor,
            UiConstructionShape.Ramp => SimulationPlacementPreviewMode.ConstructionRamp,
            _ => SimulationPlacementPreviewMode.ConstructionWall,
        };
    }

    private static SimulationPlacementPreviewRequestData Create(
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode)
    {
        return new SimulationPlacementPreviewRequestData(
            new RuntimePoint(first.X, first.Y),
            new RuntimePoint(second.X, second.Y),
            z,
            mode);
    }

    private static SimulationPlacementPreviewMode? ActivePlacementMode(UiStore ui)
    {
        return ui.PlaceMode switch
        {
            PlacementMode.HaulSecondCorner => SimulationPlacementPreviewMode.GroundItems,
            PlacementMode.MiningSecondCorner => ForMiningAction(ui.SelectedMiningAction),
            PlacementMode.ConstructionSecondCorner => ForConstructionShape(
                ui.SelectedConstructionShape),
            _ => null,
        };
    }

    private static SimulationPlacementPreviewMode? HighlightMode(
        OrderHighlight highlight,
        int currentZ)
    {
        var separator = highlight.Kind.IndexOf(':');
        var suffix = separator >= 0 && separator + 1 < highlight.Kind.Length
            ? highlight.Kind[(separator + 1)..]
            : string.Empty;
        if (highlight.Kind.StartsWith("mining", StringComparison.OrdinalIgnoreCase))
        {
            return suffix switch
            {
                nameof(UiMiningAction.DigRamp) => SimulationPlacementPreviewMode.MiningRamp,
                nameof(UiMiningAction.DigChannel) => SimulationPlacementPreviewMode.MiningChannel,
                nameof(UiMiningAction.DigStairwell) when currentZ == highlight.ZMin =>
                    SimulationPlacementPreviewMode.MiningStairwellTop,
                nameof(UiMiningAction.DigStairwell) => null,
                _ => SimulationPlacementPreviewMode.MiningDig,
            };
        }

        if (!highlight.Kind.StartsWith("construction", StringComparison.OrdinalIgnoreCase))
            return null;

        return suffix switch
        {
            nameof(UiConstructionShape.Wall) => SimulationPlacementPreviewMode.ConstructionWall,
            nameof(UiConstructionShape.Floor) => SimulationPlacementPreviewMode.ConstructionFloor,
            nameof(UiConstructionShape.Ramp) => SimulationPlacementPreviewMode.ConstructionRamp,
            _ => null,
        };
    }
}
