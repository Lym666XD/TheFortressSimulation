using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class ZoneOverlaySnapshotBuilder
{
    internal static SimulationZoneDetailData BuildDetail(World? world, int zoneId)
    {
        if (world == null)
            return SimulationZoneDetailData.Empty;

        var zone = world.Zones.Manager.GetZone(zoneId);
        if (zone == null)
            return SimulationZoneDetailData.Empty;

        var definition = world.Zones.Manager.GetDefinition(zone.DefId);
        if (definition == null)
            return SimulationZoneDetailData.Empty;

        return new SimulationZoneDetailData(
            true,
            zone.ZoneId,
            zone.Name,
            definition.DisplayName,
            definition.Category,
            zone.TotalCells,
            zone.MemberChunks.Count,
            zone.Enabled);
    }
}
