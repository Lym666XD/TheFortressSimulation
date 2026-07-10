using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class MapViewportSnapshotBuilder
{
    internal static SimulationMapViewportData Build(
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
        if (viewWidth <= 0 || viewHeight <= 0)
            return SimulationMapViewportData.Unavailable;

        zoomLevel = Math.Max(1, zoomLevel);

        var cells = new List<MapViewportCellView>(viewWidth * viewHeight);
        int maxWorldSize = fortressSize * Chunk.SIZE_XY;

        AddTerrainCells(
            cells,
            world,
            geologyCatalog,
            maxWorldSize,
            cameraPosition,
            cursorPosition,
            currentZ,
            zoomLevel,
            viewWidth,
            viewHeight,
            cursorGlyph);

        if (world != null)
            AddEntityCells(cells, world, cameraPosition, currentZ, viewWidth, viewHeight);

        return new SimulationMapViewportData(
            IsAvailable: true,
            HasWorld: world != null,
            Width: viewWidth,
            Height: viewHeight,
            CameraX: cameraPosition.X,
            CameraY: cameraPosition.Y,
            CurrentZ: currentZ,
            Cells: cells,
            Delta: SimulationMapViewportDeltaData.Unavailable);
    }
}
