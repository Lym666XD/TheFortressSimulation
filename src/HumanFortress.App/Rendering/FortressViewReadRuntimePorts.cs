using HumanFortress.App.Runtime;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed class FortressViewReadRuntimePorts
{
    private readonly IFortressRuntimeReadAccess _runtime;

    internal FortressViewReadRuntimePorts(IFortressRuntimeReadAccess runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    internal SimulationStatus SimulationStatus => _runtime.SimulationStatus;

    internal SimulationUiOverlayFrameData GetUiOverlayFrameData(
        int currentZ,
        Rectangle viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId) =>
        _runtime.GetUiOverlayFrameData(
            currentZ,
            viewport,
            showZoneOverlay,
            includeManagementDrawer,
            includeWorkDrawer,
            includeDebugMenu,
            stockpileDetailZoneId,
            zoneDetailId);

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
        int tileInspectionZ) =>
        _runtime.GetFrameRenderData(
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

    internal SimulationPlacementPreviewData GetPlacementPreviewData(
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode) =>
        _runtime.GetPlacementPreviewData(first, second, z, mode);
}
