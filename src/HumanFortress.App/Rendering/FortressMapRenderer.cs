using HumanFortress.App.UI;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static class FortressMapRenderer
{
    public static void Render(
        MapScreenSurface? mapSurface,
        SimulationMapViewportData mapData,
        NavigationOverlay? navigationOverlay,
        SimulationNavigationOverlayData navigationOverlayData)
    {
        try
        {
            if (mapSurface == null)
                return;

            mapSurface.Clear();

            if (!mapData.IsAvailable)
            {
                Logger.Log("[RenderMap] WARNING: map viewport snapshot is not available");
                return;
            }

            DrawMapCells(mapSurface, mapData);

            if (navigationOverlay != null)
            {
                navigationOverlay.RenderOverlay(mapSurface, navigationOverlayData, mapData.Viewport);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("UI.RenderMap", $"[RenderMap] ERROR: {ex.Message}", ex);
        }
    }

    private static void DrawMapCells(MapScreenSurface mapSurface, SimulationMapViewportData mapData)
    {
        int width = mapSurface.Surface.Width;
        int height = mapSurface.Surface.Height;

        foreach (var cell in mapData.Cells)
        {
            if (cell.ScreenX < 0 || cell.ScreenX >= width || cell.ScreenY < 0 || cell.ScreenY >= height)
                continue;

            mapSurface.SetGlyph(cell.ScreenX, cell.ScreenY, cell.Glyph, ToSadRogueColor(cell.Color), Color.Transparent);
        }
    }

    private static Color ToSadRogueColor(SnapshotColor color) => new(color.R, color.G, color.B);
}
