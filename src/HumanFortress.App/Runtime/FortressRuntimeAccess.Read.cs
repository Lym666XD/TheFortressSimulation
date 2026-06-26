using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
    internal SimulationStatus SimulationStatus => _read.SimulationStatus;

    internal SimulationUiOverlayFrameData GetUiOverlayFrameData(
        int currentZ,
        Rectangle viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId,
        ulong tick)
    {
        return _read.GetUiOverlayFrameData(
            currentZ,
            viewport.ToRuntimeRect(),
            showZoneOverlay,
            includeManagementDrawer,
            includeWorkDrawer,
            includeDebugMenu,
            stockpileDetailZoneId,
            zoneDetailId,
            tick);
    }

    internal SimulationPlacementPreviewData GetPlacementPreviewData(
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode)
    {
        return _read.GetPlacementPreviewData(
            first.ToRuntimePoint(),
            second.ToRuntimePoint(),
            z,
            mode);
    }

    internal SimulationFrameRenderData GetFrameRenderData(
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
        int tileInspectionZ)
    {
        return _read.GetFrameRenderData(
            includeMapViewport,
            fortressSize,
            cameraPosition.ToRuntimePoint(),
            cursorPosition.ToRuntimePoint(),
            currentZ,
            zoomLevel,
            viewWidth,
            viewHeight,
            cursorGlyph,
            navigationMode,
            selectedNavigationTarget.ToRuntimePoint(),
            tileInspectionWorldPosition.ToRuntimePoint(),
            tileInspectionZ);
    }

    SimulationStatus IFortressRuntimeReadAccess.SimulationStatus => SimulationStatus;

    SimulationUiOverlayFrameData IFortressRuntimeReadAccess.GetUiOverlayFrameData(
        int currentZ,
        Rectangle viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId,
        ulong tick) =>
        GetUiOverlayFrameData(
            currentZ,
            viewport,
            showZoneOverlay,
            includeManagementDrawer,
            includeWorkDrawer,
            includeDebugMenu,
            stockpileDetailZoneId,
            zoneDetailId,
            tick);

    SimulationFrameRenderData IFortressRuntimeReadAccess.GetFrameRenderData(
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
        int tileInspectionZ) =>
        GetFrameRenderData(
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
            tileInspectionZ);

    SimulationPlacementPreviewData IFortressRuntimeReadAccess.GetPlacementPreviewData(
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode) =>
        GetPlacementPreviewData(first, second, z, mode);
}
