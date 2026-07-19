using HumanFortress.App.Rendering;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class ZonesUI
{
    /// <summary>
    /// Render zone overlay on map (only when zone menu is open).
    /// </summary>
    public void RenderOverlay(MapScreenSurface mapSurface, SimulationZoneOverlayData overlay, RuntimeViewportGeometry viewport)
    {
        foreach (var cell in overlay.Cells)
        {
            if (!FortressViewportDrawing.TryGetLocalPosition(viewport, cell.X, cell.Y, out var local))
                continue;

            Color zoneColor = ParseColor(cell.ColorHex);
            mapSurface.Surface.SetGlyph(local.X, local.Y, cell.Glyph);
            mapSurface.Surface.SetForeground(local.X, local.Y, zoneColor);
            mapSurface.Surface.SetBackground(local.X, local.Y, new Color(zoneColor.R / 4, zoneColor.G / 4, zoneColor.B / 4, 80));
        }
    }
}
