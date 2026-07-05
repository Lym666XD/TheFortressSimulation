using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal sealed partial class FortressRuntimeAccess
{
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
}
