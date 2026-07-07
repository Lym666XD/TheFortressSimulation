using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationZoneOverlayData BuildZoneOverlaySnapshot(
        World? world,
        int currentZ,
        Rectangle viewport,
        bool showOverlay)
    {
        return ZoneOverlaySnapshotBuilder.Build(world, currentZ, viewport, showOverlay);
    }

    internal static SimulationZoneDetailData BuildZoneDetailSnapshot(World? world, int zoneId)
    {
        return ZoneOverlaySnapshotBuilder.BuildDetail(world, zoneId);
    }

    internal static ZoneHitData FindZoneAt(World? world, Point worldPosition, int z)
    {
        return ZoneOverlaySnapshotBuilder.FindAt(world, worldPosition, z);
    }

}
