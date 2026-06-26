using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeMapInspectionAccess
{
    ZoneHitData FindZoneAt(Point worldPosition, int z);
    StockpileHitData FindStockpileAt(Point worldPosition, int z);
    SimulationTileInspectionData GetTileInspectionData(Point tileWorldPosition, int tileZ);
    SimulationWorkshopDebugData GetWorkshopDebugData();
}
