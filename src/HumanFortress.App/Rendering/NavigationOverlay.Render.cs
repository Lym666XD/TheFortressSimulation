using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;

namespace HumanFortress.App.Rendering;

internal sealed partial class NavigationOverlay
{
    public void RenderOverlay(ScreenSurface surface, SimulationNavigationOverlayData overlay, RuntimeViewportGeometry viewport)
    {
        if (_currentMode == OverlayMode.None) return;
        switch (_currentMode)
        {
            case OverlayMode.PathDisplay: RenderPath(surface, viewport); break;
            default: RenderCells(surface, overlay, viewport); break;
        }
        RenderLegend(surface, overlay);
    }

    private static void RenderCells(
        ScreenSurface surface,
        SimulationNavigationOverlayData overlay,
        RuntimeViewportGeometry viewport)
    {
        var cells = overlay.Cells ?? Array.Empty<NavigationOverlayCellView>();
        foreach (var cell in cells)
        {
            FortressViewportDrawing.SetWorldCellGlyph(
                surface.Surface,
                viewport,
                cell.X,
                cell.Y,
                cell.Glyph,
                ParseColor(cell.ColorHex));
        }
    }

    private void RenderPath(ScreenSurface surface, RuntimeViewportGeometry viewport)
    {
        foreach (var cell in _currentPath.Cells)
        {
            FortressViewportDrawing.SetWorldCellGlyph(
                surface.Surface,
                viewport,
                cell.X,
                cell.Y,
                cell.Glyph,
                ParseColor(cell.ColorHex));
        }
    }
}
