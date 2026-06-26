using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationDebugMenuData GetDebugMenuData()
    {
        return _snapshots.GetDebugMenuData();
    }

    internal SimulationDebugSpawnData GetDebugSpawnData()
    {
        return _snapshots.GetDebugSpawnData();
    }

    internal SimulationWorldAvailabilityData GetWorldAvailabilityData()
    {
        return _snapshots.GetWorldAvailabilityData();
    }

    internal ZoneHitData FindZoneAt(Point worldPosition, int z)
    {
        return _snapshots.FindZoneAt(worldPosition.ToRuntimePoint(), z);
    }

    internal StockpileHitData FindStockpileAt(Point worldPosition, int z)
    {
        return _snapshots.FindStockpileAt(worldPosition.ToRuntimePoint(), z);
    }

    internal SimulationNavigationPathData FindNavigationDebugPath(
        Point start,
        int startZ,
        Point destination,
        int destinationZ)
    {
        return _snapshots.FindNavigationDebugPath(
            start.ToRuntimePoint(),
            startZ,
            destination.ToRuntimePoint(),
            destinationZ);
    }

    internal SimulationTileInspectionData GetTileInspectionData(Point tileWorldPosition, int tileZ)
    {
        return _snapshots.GetTileInspectionData(tileWorldPosition.ToRuntimePoint(), tileZ);
    }

    internal SimulationBuildCatalogData GetBuildCatalogData()
    {
        return _snapshots.GetBuildCatalogData();
    }

    internal WorkforceDebugData GetWorkforceInputData()
    {
        return _snapshots.GetWorkforceInputData();
    }

    internal SimulationWorkshopDebugData GetWorkshopDebugData()
    {
        return _snapshots.GetWorkshopDebugData();
    }

    internal WorkshopSummaryView? GetWorkshopPanelData(Guid workshopId)
    {
        return _snapshots.GetWorkshopPanelData(workshopId);
    }

    internal string? GetDefaultRecipeForWorkshop(string? workshopId)
    {
        return _snapshots.GetDefaultRecipeForWorkshop(workshopId);
    }

    SimulationDebugMenuData IFortressRuntimeUiInputAccess.GetDebugMenuData() => GetDebugMenuData();

    WorkforceDebugData IFortressRuntimeUiInputAccess.GetWorkforceInputData() => GetWorkforceInputData();

    SimulationDebugSpawnData IFortressRuntimeDebugSpawnAccess.GetDebugSpawnData() => GetDebugSpawnData();

    SimulationWorldAvailabilityData IFortressRuntimeBootstrapAccess.GetWorldAvailabilityData() => GetWorldAvailabilityData();

    SimulationWorldAvailabilityData IFortressRuntimePlacementAccess.GetWorldAvailabilityData() => GetWorldAvailabilityData();

    ZoneHitData IFortressRuntimeMapInspectionAccess.FindZoneAt(Point worldPosition, int z) => FindZoneAt(worldPosition, z);

    ZoneHitData IFortressRuntimePlacementAccess.FindZoneAt(Point worldPosition, int z) => FindZoneAt(worldPosition, z);

    StockpileHitData IFortressRuntimeMapInspectionAccess.FindStockpileAt(Point worldPosition, int z) =>
        FindStockpileAt(worldPosition, z);

    StockpileHitData IFortressRuntimePlacementAccess.FindStockpileAt(Point worldPosition, int z) =>
        FindStockpileAt(worldPosition, z);

    SimulationNavigationPathData IFortressRuntimeNavigationDebugAccess.FindNavigationDebugPath(
        Point start,
        int startZ,
        Point destination,
        int destinationZ) =>
        FindNavigationDebugPath(start, startZ, destination, destinationZ);

    SimulationTileInspectionData IFortressRuntimeMapInspectionAccess.GetTileInspectionData(Point tileWorldPosition, int tileZ) =>
        GetTileInspectionData(tileWorldPosition, tileZ);

    SimulationBuildCatalogData IFortressRuntimeBuildCatalogAccess.GetBuildCatalogData() => GetBuildCatalogData();

    SimulationWorkshopDebugData IFortressRuntimeMapInspectionAccess.GetWorkshopDebugData() => GetWorkshopDebugData();

    WorkshopSummaryView? IFortressRuntimeWorkshopPanelAccess.GetWorkshopPanelData(Guid workshopId) =>
        GetWorkshopPanelData(workshopId);

    string? IFortressRuntimeWorkshopPanelAccess.GetDefaultRecipeForWorkshop(string? workshopId) =>
        GetDefaultRecipeForWorkshop(workshopId);
}
