using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationMapViewportData BuildMapViewportSnapshot(
        World? world,
        IRuntimeGeologyCatalog? geologyCatalog,
        int fortressSize,
        Point cameraPosition,
        Point cursorPosition,
        int currentZ,
        int zoomLevel,
        int viewWidth,
        int viewHeight,
        int cursorGlyph)
    {
        return MapViewportSnapshotBuilder.Build(
            world,
            geologyCatalog,
            fortressSize,
            cameraPosition,
            cursorPosition,
            currentZ,
            zoomLevel,
            viewWidth,
            viewHeight,
            cursorGlyph);
    }
}
