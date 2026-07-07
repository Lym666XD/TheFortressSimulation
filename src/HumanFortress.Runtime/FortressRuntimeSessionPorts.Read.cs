using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime;

public interface IFortressRuntimeSessionReadPort
{
    SimulationStatus SimulationStatus { get; }

    SimulationUiOverlayFrameData GetUiOverlayFrameData(
        int currentZ,
        RuntimeRect viewport,
        bool showZoneOverlay,
        bool includeManagementDrawer,
        bool includeWorkDrawer,
        bool includeDebugMenu,
        int? stockpileDetailZoneId,
        int? zoneDetailId);

    SimulationFrameRenderData GetFrameRenderData(
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
        int tileInspectionZ);

    SimulationPlacementPreviewData GetPlacementPreviewData(
        RuntimePoint first,
        RuntimePoint second,
        int z,
        SimulationPlacementPreviewMode mode);
}
