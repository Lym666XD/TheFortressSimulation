using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Geometry;
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
        RuntimeViewportGeometry viewport,
        Point cursorPosition,
        int cursorGlyph,
        SimulationNavigationOverlayMode navigationMode,
        Point? selectedNavigationTarget,
        Point tileInspectionWorldPosition,
        int tileInspectionZ,
        SimulationSnapshotMetadata metadata)
    {
        var worldViewport = viewport.VisibleWorldRectangle();
        return new SimulationFrameRenderData(
            includeMapViewport
                ? BuildMapViewportSnapshot(
                    world,
                    geologyCatalog,
                    viewport,
                    cursorPosition,
                    cursorGlyph)
                : SimulationMapViewportData.Unavailable,
            BuildNavigationOverlaySnapshot(
                navigation,
                tuning,
                navigationMode,
                viewport.CurrentZ,
                worldViewport,
                selectedNavigationTarget),
            BuildTileInspectionSnapshot(
                world,
                geologyCatalog,
                tileInspectionWorldPosition,
                tileInspectionZ),
            metadata);
    }
}
