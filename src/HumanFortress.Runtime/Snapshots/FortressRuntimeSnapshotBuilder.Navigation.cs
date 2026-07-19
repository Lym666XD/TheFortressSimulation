using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Navigation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationNavigationOverlayData BuildNavigationOverlaySnapshot(
        NavigationManager? navigation,
        NavigationTuning? tuning,
        SimulationNavigationOverlayMode mode,
        int currentZ,
        Rectangle viewport,
        Point? selectedTarget)
    {
        return NavigationOverlaySnapshotBuilder.Build(navigation, tuning, mode, currentZ, viewport, selectedTarget);
    }

    internal static SimulationNavigationPathData FindNavigationDebugPath(
        NavigationManager? navigation,
        NavigationTuning? tuning,
        RuntimeNavigationServices? navigationServices,
        Point start,
        int startZ,
        Point destination,
        int destinationZ)
    {
        return NavigationOverlaySnapshotBuilder.FindPath(navigation, tuning, navigationServices, start, startZ, destination, destinationZ);
    }
}
