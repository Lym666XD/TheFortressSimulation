using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Geometry;
using HumanFortress.Runtime.Host;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationUiOverlayFrameData BuildUiOverlayFrameSnapshot(
        SimulationRuntimeHost<SimulationRuntimeSystems>? runtimeHost,
        World? world,
        IConstructionCatalog? constructions,
        RuntimeViewportGeometry viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId,
        SimulationSnapshotMetadata metadata)
    {
        return new SimulationUiOverlayFrameData(
            BuildCatalogSnapshot(constructions, runtimeHost?.WorkshopCategoryTags),
            BuildJobsSnapshot(runtimeHost, metadata.RuntimeTick),
            BuildWorkshopSnapshot(world, constructions, runtimeHost?.Recipes),
            BuildStockpilePresetMenuSnapshot(runtimeHost?.StockpilePresets),
            BuildStockpileOverlaySnapshot(world, viewport.CurrentZ, viewport.VisibleWorldRectangle()),
            stockpileDetailZoneId.HasValue
                ? BuildStockpileDetailSnapshot(world, stockpileDetailZoneId.Value)
                : null,
            BuildZoneOverlaySnapshot(world, viewport.CurrentZ, viewport.VisibleWorldRectangle(), showZoneOverlay),
            zoneDetailId.HasValue
                ? BuildZoneDetailSnapshot(world, zoneDetailId.Value)
                : null,
            includeManagementDrawer
                ? BuildManagementDrawerSnapshot(world)
                : null,
            includeWorkDrawer
                ? BuildWorkDrawerSnapshot(runtimeHost, world, constructions, metadata.RuntimeTick)
                : null,
            includeDebugMenu
                ? BuildDebugMenuSnapshot(world)
                : null,
            Metadata: metadata,
            Delta: SimulationUiOverlayFrameDeltaData.Unavailable,
            ZoneCatalog: BuildZoneCatalogSnapshot(world));
    }

}
