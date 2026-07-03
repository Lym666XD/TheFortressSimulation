using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal interface IFortressRuntimeReadAccess
{
    SimulationStatus SimulationStatus { get; }

    SimulationUiOverlayFrameData GetUiOverlayFrameData(
        int currentZ,
        Rectangle viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId);

    SimulationFrameRenderData GetFrameRenderData(
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
        int tileInspectionZ);

    SimulationPlacementPreviewData GetPlacementPreviewData(
        Point first,
        Point second,
        int z,
        SimulationPlacementPreviewMode mode);
}
