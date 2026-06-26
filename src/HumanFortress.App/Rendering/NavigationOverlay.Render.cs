using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal sealed partial class NavigationOverlay
{
    public void RenderOverlay(ScreenSurface surface, SimulationNavigationOverlayData overlay, Rectangle viewport)
    {
        if (_currentMode == OverlayMode.None) return;
        switch (_currentMode)
        {
            case OverlayMode.PathDisplay: RenderPath(surface, viewport); break;
            default: RenderCells(surface, overlay, viewport); break;
        }
        RenderLegend(surface, overlay);
    }

    private static void RenderCells(ScreenSurface surface, SimulationNavigationOverlayData overlay, Rectangle viewport)
    {
        var cells = overlay.Cells ?? Array.Empty<NavigationOverlayCellView>();
        foreach (var cell in cells)
        {
            int sx = cell.X - viewport.X;
            int sy = cell.Y - viewport.Y;
            if (sx < 0 || sx >= surface.Surface.Width || sy < 0 || sy >= surface.Surface.Height)
                continue;

            surface.Surface.SetGlyph(sx, sy, cell.Glyph, ParseColor(cell.ColorHex));
        }
    }

    private void RenderPath(ScreenSurface surface, Rectangle viewport)
    {
        foreach (var cell in _currentPath.Cells)
        {
            int sx = cell.X - viewport.X;
            int sy = cell.Y - viewport.Y;
            if (sx < 0 || sx >= surface.Surface.Width || sy < 0 || sy >= surface.Surface.Height)
                continue;

            surface.Surface.SetGlyph(sx, sy, cell.Glyph, ParseColor(cell.ColorHex));
        }
    }
}
