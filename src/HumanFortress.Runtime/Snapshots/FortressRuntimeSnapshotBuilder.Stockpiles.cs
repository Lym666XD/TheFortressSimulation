using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationStockpileOverlayData BuildStockpileOverlaySnapshot(
        World? world,
        int currentZ,
        Rectangle viewport)
    {
        return StockpileSnapshotBuilder.BuildOverlay(world, currentZ, viewport);
    }

    internal static SimulationStockpileDetailData BuildStockpileDetailSnapshot(World? world, int zoneId)
    {
        return StockpileSnapshotBuilder.BuildDetail(world, zoneId);
    }

    internal static StockpileHitData FindStockpileAt(World? world, Point worldPosition, int z)
    {
        return StockpileSnapshotBuilder.FindAt(world, worldPosition, z);
    }
}
