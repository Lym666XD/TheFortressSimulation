using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal ZoneHitData FindZoneAt(Point worldPosition, int z)
    {
        return _snapshots.FindZoneAt(worldPosition.ToRuntimePoint(), z);
    }

    internal StockpileHitData FindStockpileAt(Point worldPosition, int z)
    {
        return _snapshots.FindStockpileAt(worldPosition.ToRuntimePoint(), z);
    }

    internal SimulationTileInspectionData GetTileInspectionData(Point tileWorldPosition, int tileZ)
    {
        return _snapshots.GetTileInspectionData(tileWorldPosition.ToRuntimePoint(), tileZ);
    }

    internal SimulationWorkshopDebugData GetWorkshopDebugData()
    {
        return _snapshots.GetWorkshopDebugData();
    }

    ZoneHitData IFortressRuntimeMapInspectionAccess.FindZoneAt(Point worldPosition, int z) =>
        FindZoneAt(worldPosition, z);

    ZoneHitData IFortressRuntimePlacementQueryAccess.FindZoneAt(Point worldPosition, int z) =>
        FindZoneAt(worldPosition, z);

    StockpileHitData IFortressRuntimeMapInspectionAccess.FindStockpileAt(Point worldPosition, int z) =>
        FindStockpileAt(worldPosition, z);

    StockpileHitData IFortressRuntimePlacementQueryAccess.FindStockpileAt(Point worldPosition, int z) =>
        FindStockpileAt(worldPosition, z);

    SimulationTileInspectionData IFortressRuntimeMapInspectionAccess.GetTileInspectionData(Point tileWorldPosition, int tileZ) =>
        GetTileInspectionData(tileWorldPosition, tileZ);

    SimulationWorkshopDebugData IFortressRuntimeMapInspectionAccess.GetWorkshopDebugData() => GetWorkshopDebugData();
}
