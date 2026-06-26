using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeNavigationDebugAccess
{
    SimulationNavigationPathData FindNavigationDebugPath(
        Point start,
        int startZ,
        Point destination,
        int destinationZ);
}
