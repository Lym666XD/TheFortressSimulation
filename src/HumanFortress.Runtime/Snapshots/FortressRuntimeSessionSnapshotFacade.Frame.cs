using HumanFortress.Contracts.Runtime;
using SadRogue.Primitives;
using HumanFortress.Runtime.Session;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSessionSnapshotFacade
{
    internal static SimulationUiOverlayFrameData BuildUiOverlayFrameSnapshot(
        FortressRuntimeSession? session,
        RuntimeViewportGeometry viewport,
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
        RuntimeViewportGeometry viewport,
        Point cursorPosition,
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
            viewport,
            cursorPosition,
            cursorGlyph,
            navigationMode,
            selectedNavigationTarget,
            tileInspectionWorldPosition,
            tileInspectionZ,
            metadata);
    }
}
