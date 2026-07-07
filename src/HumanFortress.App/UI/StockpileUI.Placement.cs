using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class StockpileUI
{
    /// <summary>
    /// Draw placement mode UI.
    /// </summary>
    public void DrawPlacementMode(ScreenSurface surface, UiStore ui, Point mouseWorld)
    {
        var statusY = surface.Height - 2;

        switch (ui.PlaceMode)
        {
            case PlacementMode.StockpileFirstCorner:
                surface.Print(2, statusY, "[STOCKPILE] Click first corner - ESC to cancel", Color.Yellow);
                break;

            case PlacementMode.StockpileSecondCorner:
                if (ui.PlaceFirstCorner.HasValue)
                {
                    var size = CalculateRectSize(ui.PlaceFirstCorner.Value, mouseWorld);
                    surface.Print(2, statusY,
                        $"[STOCKPILE] Click opposite corner - {size.x}x{size.y} = {size.x * size.y} tiles - ESC to cancel",
                        Color.Yellow);
                }
                break;

            case PlacementMode.StockpilePresetSelect:
                DrawPresetSelection(surface);
                break;

            case PlacementMode.StockpileDelete:
                surface.Print(2, statusY, "[DELETE] Click a stockpile to remove it - ESC to cancel", Color.Red);
                break;

            case PlacementMode.StockpileCopy:
                surface.Print(2, statusY, "[COPY] Click a stockpile to copy its settings - ESC to cancel", Color.Cyan);
                break;
        }
    }

    private static (int x, int y) CalculateRectSize(Point p1, Point p2)
    {
        return (Math.Abs(p2.X - p1.X) + 1, Math.Abs(p2.Y - p1.Y) + 1);
    }
}
