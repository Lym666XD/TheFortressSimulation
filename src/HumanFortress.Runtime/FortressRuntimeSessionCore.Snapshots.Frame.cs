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
        int? zoneDetailId)
    {
        return _frameSnapshots.PublishUiOverlayFrame(
            _runtimeSession,
            _services.TickScheduler.CurrentTick,
            allowCache: !_services.TickScheduler.IsRunning,
            new RuntimeUiOverlayFrameRequest(
                currentZ,
                viewport,
                showZoneOverlay,
                includeManagementDrawer,
                includeWorkDrawer,
                includeDebugMenu,
                stockpileDetailZoneId,
                zoneDetailId));
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
        return _frameSnapshots.PublishFrameRender(
            _runtimeSession,
            _services.TickScheduler.CurrentTick,
            allowCache: !_services.TickScheduler.IsRunning,
            new RuntimeFrameRenderRequest(
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
                tileInspectionZ));
    }
}
