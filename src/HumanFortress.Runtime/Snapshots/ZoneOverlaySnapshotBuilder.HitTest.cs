using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class ZoneOverlaySnapshotBuilder
{
    internal static ZoneHitData FindAt(World? world, Point worldPosition, int z)
    {
        if (world == null)
            return ZoneHitData.Empty;

        int zoneId = world.Zones.GetZoneAtPosition(worldPosition.X, worldPosition.Y, z);
        return zoneId <= 0
            ? ZoneHitData.Empty
            : new ZoneHitData(true, zoneId, worldPosition.X, worldPosition.Y, z);
    }
}
