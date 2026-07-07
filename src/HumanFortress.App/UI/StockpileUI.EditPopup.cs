using System;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class StockpileUI
{
    /// <summary>
    /// Draw edit popup for a stockpile.
    /// </summary>
    public void DrawEditPopup(ScreenSurface surface, SimulationStockpileDetailData detail)
    {
        if (!_editPopupOpen || !_editingZoneId.HasValue)
            return;

        if (!detail.HasZone)
        {
            CloseEditPopup();
            return;
        }

        int x = Math.Max(2, Math.Min(_editPopupPos.X, surface.Width - 32));
        int y = Math.Max(2, Math.Min(_editPopupPos.Y, surface.Height - 12));

        var bg = Color.Black.SetAlpha(230);
        var fg = Color.White;
        var highlight = Color.Cyan;

        DrawBox(surface, x, y, 30, 10, fg, bg);
        surface.Print(x + 1, y, $" {detail.Name} ", highlight);

        surface.Print(x + 2, y + 2, $"Capacity: {detail.UsedCells}/{detail.TotalCells}", fg);
        surface.Print(x + 2, y + 3, $"Priority: {detail.PriorityName}", fg);
        surface.Print(x + 2, y + 4, $"Filter: {detail.FilterSummary}", fg);

        surface.Print(x + 2, y + 6, "[R] Rename", fg);
        surface.Print(x + 2, y + 7, "[P] Change Priority", fg);
        surface.Print(x + 2, y + 8, "[F] Change Filter", fg);
        surface.Print(x + 16, y + 6, "[D] Delete Zone", Color.Red);
        surface.Print(x + 16, y + 8, "[ESC] Close", Color.Gray);
    }

    /// <summary>
    /// Handle click on stockpile for editing.
    /// </summary>
    public bool TryOpenStockpileAt(Point worldPos, StockpileHitData hit)
    {
        if (!hit.HasZone)
            return false;

        OpenEditPopup(hit.ZoneId, worldPos);
        return true;
    }

    public void CloseEditPopup()
    {
        _editPopupOpen = false;
        _editingZoneId = null;
    }

    private void OpenEditPopup(int zoneId, Point worldPos)
    {
        _editPopupOpen = true;
        _editingZoneId = zoneId;
        _editPopupPos = worldPos;
    }
}
