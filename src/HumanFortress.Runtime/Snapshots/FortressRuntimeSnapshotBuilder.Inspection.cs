using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationTileInspectionData BuildTileInspectionSnapshot(
        World? world,
        IRuntimeGeologyCatalog? geologyCatalog,
        Point tileWorldPosition,
        int tileZ)
    {
        return TileInspectionSnapshotBuilder.Build(world, geologyCatalog, tileWorldPosition, tileZ);
    }
}
