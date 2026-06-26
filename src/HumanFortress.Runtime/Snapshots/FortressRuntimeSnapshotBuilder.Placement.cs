using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationPlacementPreviewData BuildPlacementPreviewSnapshot(
        World? world,
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode)
    {
        return PlacementPreviewSnapshotBuilder.Build(world, first, second, z, mode);
    }
}
