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
        return _snapshots.FindNavigationDebugPath(
            start.ToRuntimePoint(),
            startZ,
            destination.ToRuntimePoint(),
            destinationZ);
    }

    SimulationNavigationPathData IFortressRuntimeNavigationDebugAccess.FindNavigationDebugPath(
        Point start,
        int startZ,
        Point destination,
        int destinationZ) =>
        FindNavigationDebugPath(start, startZ, destination, destinationZ);
}
