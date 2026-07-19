using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationNavigationPathData FindNavigationDebugPath(
        Point start,
        int startZ,
        Point destination,
        int destinationZ)
    {
        var request = new SimulationNavigationPathRequestData(
            start.ToRuntimePoint(),
            startZ,
            destination.ToRuntimePoint(),
            destinationZ);
        return TryGetCommittedFrame(out var committed)
            && committed.Frame.NavigationPath.IsAvailable
            && committed.Frame.NavigationPath.Request == request
                ? committed.Frame.NavigationPath.Path
                : SimulationNavigationPathData.Unavailable;
    }

    SimulationNavigationPathData IFortressRuntimeNavigationDebugAccess.FindNavigationDebugPath(
        Point start,
        int startZ,
        Point destination,
        int destinationZ) =>
        FindNavigationDebugPath(start, startZ, destination, destinationZ);
}
