using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class MapViewportSnapshotBuilder
{
    internal static SimulationMapViewportData Build(
        World? world,
        IRuntimeGeologyCatalog? geologyCatalog,
        RuntimeViewportGeometry viewport,
        Point cursorPosition,
        int cursorGlyph)
    {
        viewport = RuntimeViewportGeometryMath.Normalize(viewport);
        if (viewport.Surface.Width <= 0 || viewport.Surface.Height <= 0)
            return SimulationMapViewportData.Unavailable;

        var cells = new List<MapViewportCellView>(viewport.Surface.Width * viewport.Surface.Height);

        AddTerrainCells(
            cells,
            world,
            geologyCatalog,
            viewport,
            cursorPosition,
            cursorGlyph);

        if (world != null)
            AddEntityCells(cells, world, viewport);

        return new SimulationMapViewportData(
            IsAvailable: true,
            HasWorld: world != null,
            Width: viewport.Surface.Width,
            Height: viewport.Surface.Height,
            CameraX: viewport.CameraWorldOrigin.X,
            CameraY: viewport.CameraWorldOrigin.Y,
            CurrentZ: viewport.CurrentZ,
            Cells: cells,
            Delta: SimulationMapViewportDeltaData.Unavailable,
            Viewport: viewport);
    }
}
