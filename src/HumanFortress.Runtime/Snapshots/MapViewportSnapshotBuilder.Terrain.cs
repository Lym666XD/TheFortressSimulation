using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class MapViewportSnapshotBuilder
{
    private static void AddTerrainCells(
        List<MapViewportCellView> cells,
        World? world,
        IRuntimeGeologyCatalog? geologyCatalog,
        RuntimeViewportGeometry viewport,
        Point cursorPosition,
        int cursorGlyph)
    {
        int visibleWorldWidth = RuntimeViewportGeometryMath.VisibleWorldWidth(viewport);
        int visibleWorldHeight = RuntimeViewportGeometryMath.VisibleWorldHeight(viewport);
        for (int worldOffsetX = 0; worldOffsetX < visibleWorldWidth; worldOffsetX++)
        {
            for (int worldOffsetY = 0; worldOffsetY < visibleWorldHeight; worldOffsetY++)
            {
                int worldX = viewport.CameraWorldOrigin.X + worldOffsetX;
                int worldY = viewport.CameraWorldOrigin.Y + worldOffsetY;
                bool isCursor = worldX == cursorPosition.X && worldY == cursorPosition.Y;

                var (glyph, color) = GetTerrainDisplay(
                    world,
                    geologyCatalog,
                    viewport.WorldBounds,
                    worldX,
                    worldY,
                    viewport.CurrentZ);

                int firstScreenX = worldOffsetX * viewport.ZoomLevel;
                int firstScreenY = worldOffsetY * viewport.ZoomLevel;
                int lastScreenX = Math.Min(viewport.Surface.Width, firstScreenX + viewport.ZoomLevel);
                int lastScreenY = Math.Min(viewport.Surface.Height, firstScreenY + viewport.ZoomLevel);
                for (int screenX = firstScreenX; screenX < lastScreenX; screenX++)
                {
                    for (int screenY = firstScreenY; screenY < lastScreenY; screenY++)
                    {
                        cells.Add(new MapViewportCellView(
                            screenX,
                            screenY,
                            isCursor ? cursorGlyph : glyph,
                            (isCursor ? Color.Yellow : color).ToSnapshotColor()));
                    }
                }
            }
        }
    }

    private static (int Glyph, Color Color) GetTerrainDisplay(
        World? world,
        IRuntimeGeologyCatalog? geologyCatalog,
        RuntimeWorldBounds worldBounds,
        int worldX,
        int worldY,
        int currentZ)
    {
        if (!worldBounds.Contains(worldX, worldY, currentZ))
            return ('#', Color.DarkGray);

        if (world == null)
            return ('?', Color.DarkGray);

        var tile = world.GetTile(worldX, worldY, currentZ);
        if (!tile.HasValue)
            return ('#', Color.DarkGray);

        return GetTileDisplay(tile.Value, geologyCatalog);
    }

}
