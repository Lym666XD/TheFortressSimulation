using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime.Snapshots;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    SimulationUiOverlayFrameData IFortressRuntimeSessionReadPort.GetUiOverlayFrameData(
        int currentZ,
        RuntimeRect viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId,
        ulong tick)
    {
        return FortressRuntimeSessionSnapshotFacade.BuildUiOverlayFrameSnapshot(
            _runtimeSession,
            currentZ,
            viewport.ToSadRogueRectangle(),
            showZoneOverlay,
            includeManagementDrawer,
            includeWorkDrawer,
            includeDebugMenu,
            stockpileDetailZoneId,
            zoneDetailId,
            tick);
    }

    SimulationFrameRenderData IFortressRuntimeSessionReadPort.GetFrameRenderData(
        bool includeMapViewport,
        int fortressSize,
        RuntimePoint cameraPosition,
        RuntimePoint cursorPosition,
        int currentZ,
        int zoomLevel,
        int viewWidth,
        int viewHeight,
        int cursorGlyph,
        SimulationNavigationOverlayMode navigationMode,
        RuntimePoint? selectedNavigationTarget,
        RuntimePoint tileInspectionWorldPosition,
        int tileInspectionZ)
    {
        return FortressRuntimeSessionSnapshotFacade.BuildFrameRenderSnapshot(
            _runtimeSession,
            includeMapViewport,
            fortressSize,
            cameraPosition.ToSadRoguePoint(),
            cursorPosition.ToSadRoguePoint(),
            currentZ,
            zoomLevel,
            viewWidth,
            viewHeight,
            cursorGlyph,
            navigationMode,
            selectedNavigationTarget.ToSadRoguePoint(),
            tileInspectionWorldPosition.ToSadRoguePoint(),
            tileInspectionZ);
    }
}
