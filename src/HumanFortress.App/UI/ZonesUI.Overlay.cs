using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class ZonesUI
{
    /// <summary>
    /// Render zone overlay on map (only when zone menu is open).
    /// </summary>
    public void RenderOverlay(MapScreenSurface mapSurface, SimulationZoneOverlayData overlay, Rectangle viewport)
    {
        foreach (var cell in overlay.Cells)
        {
            int sx = cell.X - viewport.X;
            int sy = cell.Y - viewport.Y;
            if (sx < 0 || sx >= mapSurface.Surface.Width || sy < 0 || sy >= mapSurface.Surface.Height)
                continue;

            Color zoneColor = ParseColor(cell.ColorHex);
            mapSurface.Surface.SetGlyph(sx, sy, cell.Glyph);
            mapSurface.Surface.SetForeground(sx, sy, zoneColor);
            mapSurface.Surface.SetBackground(sx, sy, new Color(zoneColor.R / 4, zoneColor.G / 4, zoneColor.B / 4, 80));
        }
    }
}
