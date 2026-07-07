using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class MapViewportSnapshotBuilder
{
    private static void AddTerrainCells(
        List<MapViewportCellView> cells,
        World? world,
        IRuntimeGeologyCatalog? geologyCatalog,
        int maxWorldSize,
        Point cameraPosition,
        Point cursorPosition,
        int currentZ,
        int zoomLevel,
        int viewWidth,
        int viewHeight,
        int cursorGlyph)
    {
        for (int screenX = 0; screenX < viewWidth; screenX++)
        {
            for (int screenY = 0; screenY < viewHeight; screenY++)
            {
                int worldX = cameraPosition.X + (screenX / zoomLevel);
                int worldY = cameraPosition.Y + (screenY / zoomLevel);
                bool isCursor = worldX == cursorPosition.X && worldY == cursorPosition.Y;

                var (glyph, color) = GetTerrainDisplay(
                    world,
                    geologyCatalog,
                    maxWorldSize,
                    worldX,
                    worldY,
                    currentZ);

                cells.Add(new MapViewportCellView(
                    screenX,
                    screenY,
                    isCursor ? cursorGlyph : glyph,
                    (isCursor ? Color.Yellow : color).ToSnapshotColor()));
            }
        }
    }

    private static (int Glyph, Color Color) GetTerrainDisplay(
        World? world,
        IRuntimeGeologyCatalog? geologyCatalog,
        int maxWorldSize,
        int worldX,
        int worldY,
        int currentZ)
    {
        if (worldX < 0 || worldX >= maxWorldSize || worldY < 0 || worldY >= maxWorldSize)
            return ('#', Color.DarkGray);

        if (world == null)
            return ('?', Color.DarkGray);

        var tile = world.GetTile(worldX, worldY, currentZ);
        if (!tile.HasValue)
            return ('#', Color.DarkGray);

        return GetTileDisplay(tile.Value, geologyCatalog);
    }

}
