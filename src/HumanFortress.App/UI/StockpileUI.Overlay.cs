using System;
using HumanFortress.App.Rendering;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class StockpileUI
{
    /// <summary>
    /// Render stockpile overlays on the map.
    /// </summary>
    public void RenderOverlay(ScreenSurface mapSurface, SimulationStockpileOverlayData overlay, RuntimeViewportGeometry viewport)
    {
        foreach (var cell in overlay.Cells)
        {
            FortressViewportDrawing.SetWorldCellGlyph(
                mapSurface.Surface,
                viewport,
                cell.X,
                cell.Y,
                'S',
                Color.Green.SetAlpha(150));
        }
    }

    /// <summary>
    /// Render preview rectangle during placement.
    /// </summary>
    public void RenderPlacementPreview(
        ScreenSurface mapSurface,
        Point corner1,
        Point corner2,
        RuntimeViewportGeometry viewport,
        bool valid)
    {
        var rect = CreateRectangle(corner1, corner2);
        if (!valid) return;
        var gold = new Color(255, 230, 0);
        for (int x = rect.X; x < rect.X + rect.Width; x++)
        {
            for (int y = rect.Y; y < rect.Y + rect.Height; y++)
            {
                FortressViewportDrawing.SetWorldCellGlyph(
                    mapSurface.Surface,
                    viewport,
                    x,
                    y,
                    '.',
                    gold);
            }
        }
    }

    private static Rectangle CreateRectangle(Point p1, Point p2)
    {
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Abs(p2.X - p1.X) + 1;
        int h = Math.Abs(p2.Y - p1.Y) + 1;
        return new Rectangle(x, y, w, h);
    }
}
