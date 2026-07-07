using System;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class OrdersUI
{
    public void RenderPlacementPreview(
        MapScreenSurface mapSurface,
        SimulationPlacementPreviewData preview,
        Rectangle viewport,
        bool show,
        bool showEligibleHint)
    {
        if (!show) return;
        var surf = mapSurface.Surface;
        var gold = new Color(255, 230, 0);

        foreach (var cell in preview.Cells)
        {
            int sx = cell.X - viewport.X;
            int sy = cell.Y - viewport.Y;
            if (sx >= 0 && sx < surf.Width && sy >= 0 && sy < surf.Height)
                surf.SetGlyph(sx, sy, '.', gold, Color.Transparent);
        }

        if (!showEligibleHint)
            return;

        int labelX = preview.X - viewport.X;
        int labelY = preview.Y - viewport.Y - 1;
        if (labelY < 0) labelY = preview.Y - viewport.Y + preview.Height;
        if (labelX < 0) labelX = 0;
        if (labelX + 14 < surf.Width && labelY >= 0 && labelY < surf.Height)
            surf.Print(labelX, labelY, $"{preview.EligibleCells}/{preview.TotalCells} eligible", Color.Cyan);
    }

    public void DrawPlacementMode(ScreenSurface surface, UiStore ui, Point mouseWorld)
    {
        var statusY = surface.Height - 2;
        switch (ui.PlaceMode)
        {
            case PlacementMode.HaulFirstCorner:
                surface.Print(2, statusY, "[HAUL] Click first corner - ESC to cancel", Color.Yellow);
                break;
            case PlacementMode.HaulSecondCorner:
                if (ui.PlaceFirstCorner.HasValue)
                {
                    var size = (Math.Abs(mouseWorld.X - ui.PlaceFirstCorner.Value.X) + 1,
                        Math.Abs(mouseWorld.Y - ui.PlaceFirstCorner.Value.Y) + 1);
                    surface.Print(2, statusY,
                        $"[HAUL] Click opposite corner - {size.Item1}x{size.Item2} tiles - ESC to cancel",
                        Color.Yellow);
                }
                break;
            case PlacementMode.MiningFirstCorner:
                surface.Print(2, statusY, $"[MINING] Click first corner  Z-range: {ui.PlaceZMin}..{ui.PlaceZMax} - ESC to cancel", Color.Cyan);
                break;
            case PlacementMode.MiningSecondCorner:
                if (ui.PlaceFirstCorner.HasValue)
                {
                    var size = (Math.Abs(mouseWorld.X - ui.PlaceFirstCorner.Value.X) + 1,
                        Math.Abs(mouseWorld.Y - ui.PlaceFirstCorner.Value.Y) + 1);
                    surface.Print(2, statusY,
                        $"[MINING] Click opposite corner - {size.Item1}x{size.Item2} tiles  Z-range: {ui.PlaceZMin}..{ui.PlaceZMax} - ESC to cancel",
                        Color.Cyan);
                }
                break;
            case PlacementMode.ConstructionFirstCorner:
                surface.Print(2, statusY, "[BUILD] Click first corner - ESC to cancel", Color.Yellow);
                break;
            case PlacementMode.ConstructionSecondCorner:
                if (ui.PlaceFirstCorner.HasValue)
                {
                    var size = (Math.Abs(mouseWorld.X - ui.PlaceFirstCorner.Value.X) + 1,
                        Math.Abs(mouseWorld.Y - ui.PlaceFirstCorner.Value.Y) + 1);
                    int sx = size.Item1 < 0 ? 0 : size.Item1;
                    int sy = size.Item2 < 0 ? 0 : size.Item2;
                    surface.Print(2, statusY,
                        $"[BUILD] Click opposite corner - {sx}x{sy} tiles - ESC to cancel",
                        Color.Yellow);
                }
                break;
        }
    }
}
