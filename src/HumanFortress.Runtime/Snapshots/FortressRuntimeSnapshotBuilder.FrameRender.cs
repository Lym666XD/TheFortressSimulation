using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Navigation;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationFrameRenderData BuildFrameRenderSnapshot(
        World? world,
        IRuntimeGeologyCatalog? geologyCatalog,
        NavigationManager? navigation,
        NavigationTuning? tuning,
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
        var viewport = new Rectangle(cameraPosition.X, cameraPosition.Y, viewWidth, viewHeight);
        return new SimulationFrameRenderData(
            includeMapViewport
                ? BuildMapViewportSnapshot(
                    world,
                    geologyCatalog,
                    fortressSize,
                    cameraPosition,
                    cursorPosition,
                    currentZ,
                    zoomLevel,
                    viewWidth,
                    viewHeight,
                    cursorGlyph)
                : SimulationMapViewportData.Unavailable,
            BuildNavigationOverlaySnapshot(
                navigation,
                tuning,
                navigationMode,
                currentZ,
                viewport,
                selectedNavigationTarget),
            BuildTileInspectionSnapshot(
                world,
                geologyCatalog,
                tileInspectionWorldPosition,
                tileInspectionZ),
            metadata);
    }
}
