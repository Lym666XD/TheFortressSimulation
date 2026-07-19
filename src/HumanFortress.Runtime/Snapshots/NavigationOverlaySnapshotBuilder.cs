using HumanFortress.Contracts.Navigation;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Navigation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class NavigationOverlaySnapshotBuilder
{
    private const string Green = "#00FF00";
    private const string Blue = "#0000FF";
    private const string Gray = "#808080";
    private const string DarkGray = "#404040";
    private const string DarkRed = "#8B0000";
    private const string YellowGreen = "#9ACD32";
    private const string Yellow = "#FFFF00";
    private const string Orange = "#FFA500";
    private const string Red = "#FF0000";
    private const string Cyan = "#00FFFF";

    internal static SimulationNavigationPathData FindPath(
        NavigationManager? navigation,
        NavigationTuning? tuning,
        RuntimeNavigationServices? navigationServices,
        Point start,
        int startZ,
        Point destination,
        int destinationZ)
    {
        if (navigation == null)
            return SimulationNavigationPathData.Unavailable;

        var activeTuning = tuning ?? NavigationTuning.Default;
        var flags = activeTuning.AllowDiagonals ? PathFlags.AllowDiagonal : PathFlags.None;
        var request = new PathRequest(
            new Point3(start.X, start.Y, startZ),
            new Point3(destination.X, destination.Y, destinationZ),
            MoveMode.Walk,
            flags,
            0);

        var path = (navigationServices ?? new RuntimeNavigationServices(null, activeTuning)).FindPath(navigation, in request);
        return new SimulationNavigationPathData(
            true,
            path.Kind.ToString(),
            path.Length,
            path.TotalCost,
            BuildPathCells(path));
    }

    internal static SimulationNavigationOverlayData Build(
        NavigationManager? navigation,
        NavigationTuning? tuning,
        SimulationNavigationOverlayMode mode,
        int currentZ,
        Rectangle viewport,
        Point? selectedTarget)
    {
        var activeTuning = tuning ?? NavigationTuning.Default;
        if (mode == SimulationNavigationOverlayMode.None
            || mode == SimulationNavigationOverlayMode.PathDisplay
            || navigation == null)
        {
            return SimulationNavigationOverlayData.EmptyFor(mode, activeTuning.BaseCost);
        }

        var cells = mode switch
        {
            SimulationNavigationOverlayMode.Walkability => BuildWalkability(navigation, currentZ, viewport),
            SimulationNavigationOverlayMode.MovementCost => BuildMovementCost(navigation, activeTuning, currentZ, viewport),
            SimulationNavigationOverlayMode.Traffic => BuildTraffic(navigation, currentZ, viewport),
            SimulationNavigationOverlayMode.Connectivity => BuildConnectivity(navigation, currentZ, viewport),
            SimulationNavigationOverlayMode.FlowField => BuildFlowField(navigation, currentZ, viewport, selectedTarget),
            SimulationNavigationOverlayMode.RampMask => BuildRampMask(navigation, currentZ, viewport),
            _ => Array.Empty<NavigationOverlayCellView>(),
        };

        return cells.Count == 0
            ? SimulationNavigationOverlayData.EmptyFor(mode, activeTuning.BaseCost)
            : new SimulationNavigationOverlayData(mode, cells, activeTuning.BaseCost);
    }
}
