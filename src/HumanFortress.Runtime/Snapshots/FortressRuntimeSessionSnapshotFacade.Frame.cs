using SadRogue.Primitives;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    internal static SimulationUiOverlayFrameData BuildUiOverlayFrameSnapshot(
        FortressRuntimeSession? session,
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
        return FortressRuntimeSnapshotBuilder.BuildUiOverlayFrameSnapshot(
            Host(session),
            World(session),
            Constructions(session),
            currentZ,
            viewport,
            showZoneOverlay,
            includeManagementDrawer,
            includeWorkDrawer,
            includeDebugMenu,
            stockpileDetailZoneId,
            zoneDetailId,
            metadata);
    }

    internal static SimulationFrameRenderData BuildFrameRenderSnapshot(
        FortressRuntimeSession? session,
        bool includeMapViewport,
        int fortressSize,
        Point cameraPosition,
        Point cursorPosition,
        int currentZ,
        int zoomLevel,
        int viewWidth,
        int viewHeight,
        int cursorGlyph,
        SimulationNavigationOverlayMode navigationMode,
        Point? selectedNavigationTarget,
        Point tileInspectionWorldPosition,
        int tileInspectionZ,
        SimulationSnapshotMetadata metadata)
    {
        return FortressRuntimeSnapshotBuilder.BuildFrameRenderSnapshot(
            World(session),
            Geology(session),
            Navigation(session),
            NavigationTuning(session),
            includeMapViewport,
            fortressSize,
            cameraPosition,
            cursorPosition,
            currentZ,
            zoomLevel,
            viewWidth,
            viewHeight,
            cursorGlyph,
            navigationMode,
            selectedNavigationTarget,
            tileInspectionWorldPosition,
            tileInspectionZ,
            metadata);
    }
}
