using System;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class StockpileUI
{
    /// <summary>
    /// Render stockpile overlays on the map.
    /// </summary>
    public void RenderOverlay(ScreenSurface mapSurface, SimulationStockpileOverlayData overlay, Rectangle viewport)
    {
        foreach (var cell in overlay.Cells)
        {
            int screenX = cell.X - viewport.X;
            int screenY = cell.Y - viewport.Y;

            if (screenX >= 0 && screenX < mapSurface.Width &&
                screenY >= 0 && screenY < mapSurface.Height)
            {
                mapSurface.SetGlyph(screenX, screenY, 'S', Color.Green.SetAlpha(150));
            }
        }
    }

    /// <summary>
    /// Render preview rectangle during placement.
    /// </summary>
    public void RenderPlacementPreview(
        ScreenSurface mapSurface,
        Point corner1,
        Point corner2,
        Rectangle viewport,
        bool valid)
    {
        var rect = CreateRectangle(corner1, corner2);
        if (!valid) return;
        var gold = new Color(255, 230, 0);
        for (int x = rect.X; x < rect.X + rect.Width; x++)
        {
            for (int y = rect.Y; y < rect.Y + rect.Height; y++)
            {
                var screenX = x - viewport.X;
                var screenY = y - viewport.Y;
                if (screenX >= 0 && screenX < mapSurface.Width && screenY >= 0 && screenY < mapSurface.Height)
                    mapSurface.SetGlyph(screenX, screenY, '.', gold, Color.Transparent);
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
