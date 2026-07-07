using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime;

public interface IFortressRuntimeSessionSnapshotPort
{
    SimulationBuildCatalogData GetBuildCatalogData();
    SimulationDebugMenuData GetDebugMenuData();
    SimulationDebugSpawnData GetDebugSpawnData();
    SimulationWorldAvailabilityData GetWorldAvailabilityData();
    ZoneHitData FindZoneAt(RuntimePoint worldPosition, int z);
    StockpileHitData FindStockpileAt(RuntimePoint worldPosition, int z);

    SimulationNavigationPathData FindNavigationDebugPath(
        RuntimePoint start,
        int startZ,
        RuntimePoint destination,
        int destinationZ);

    SimulationTileInspectionData GetTileInspectionData(RuntimePoint tileWorldPosition, int tileZ);
    WorkforceDebugData GetWorkforceInputData();
    SimulationWorkshopDebugData GetWorkshopDebugData();
    WorkshopSummaryView? GetWorkshopPanelData(Guid workshopId);
    string? GetDefaultRecipeForWorkshop(string? workshopId);
}
