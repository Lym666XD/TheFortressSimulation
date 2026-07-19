using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Runtime.Snapshots;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    SimulationAppFrameData IFortressRuntimeSessionReadPort.GetCommittedAppFrame(
        SimulationAppFrameRequestData request)
    {
        return GetCommittedAppFrameCore(request);
    }

    SimulationUiOverlayFrameData IFortressRuntimeSessionSnapshotPort.GetUiOverlayFrameData(
        RuntimeViewportGeometry viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId)
    {
        viewport = NormalizeViewport(viewport);
        return _frameSnapshots.PublishUiOverlayFrame(
            _runtimeSession,
            _services.TickScheduler.CurrentTick,
            allowCache: !_services.TickScheduler.IsRunning,
            new RuntimeUiOverlayFrameRequest(
                viewport,
                showZoneOverlay,
                includeManagementDrawer,
                includeWorkDrawer,
                includeDebugMenu,
                stockpileDetailZoneId,
                zoneDetailId));
    }

    SimulationFrameRenderData IFortressRuntimeSessionSnapshotPort.GetFrameRenderData(
        bool includeMapViewport,
        RuntimeViewportGeometry viewport,
        RuntimePoint cursorPosition,
        int cursorGlyph,
        SimulationNavigationOverlayMode navigationMode,
        RuntimePoint? selectedNavigationTarget,
        RuntimePoint tileInspectionWorldPosition,
        int tileInspectionZ)
    {
        viewport = NormalizeViewport(viewport);
        return _frameSnapshots.PublishFrameRender(
            _runtimeSession,
            _services.TickScheduler.CurrentTick,
            allowCache: !_services.TickScheduler.IsRunning,
            new RuntimeFrameRenderRequest(
                includeMapViewport,
                viewport,
                cursorPosition,
                cursorGlyph,
                navigationMode,
                selectedNavigationTarget,
                tileInspectionWorldPosition,
                tileInspectionZ));
    }

    private RuntimeViewportGeometry NormalizeViewport(RuntimeViewportGeometry viewport)
    {
        var world = _runtimeSession?.World;
        var worldBounds = world == null
            ? RuntimeWorldBounds.Empty
            : new RuntimeWorldBounds(0, 0, world.SizeInTiles, world.SizeInTiles, 0, world.MaxZ);
        return RuntimeViewportGeometryMath.Normalize(viewport with { WorldBounds = worldBounds });
    }
}
