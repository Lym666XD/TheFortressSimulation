using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationUiOverlayFrameData BuildUiOverlayFrameSnapshot(
        SimulationRuntimeHost<SimulationRuntimeSystems>? runtimeHost,
        World? world,
        IConstructionCatalog? constructions,
        int currentZ,
        Rectangle viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId,
        SimulationSnapshotMetadata metadata)
    {
        return new SimulationUiOverlayFrameData(
            BuildCatalogSnapshot(constructions),
            BuildJobsSnapshot(runtimeHost, metadata.RuntimeTick),
            BuildWorkshopSnapshot(world, constructions),
            BuildStockpilePresetMenuSnapshot(runtimeHost?.StockpilePresets),
            BuildStockpileOverlaySnapshot(world, currentZ, viewport),
            stockpileDetailZoneId.HasValue
                ? BuildStockpileDetailSnapshot(world, stockpileDetailZoneId.Value)
                : null,
            BuildZoneOverlaySnapshot(world, currentZ, viewport, showZoneOverlay),
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
            Delta: SimulationUiOverlayFrameDeltaData.Unavailable);
    }

}
