using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class ZonesUI
{
    /// <summary>
    /// Draw zone detail popup.
    /// </summary>
    public void DrawDetailPopup(ScreenSurface surface, SimulationZoneDetailData detail)
    {
        if (!_detailPopupZoneId.HasValue) return;

        if (!detail.HasZone)
        {
            _detailPopupZoneId = null;
            return;
        }

        var surf = surface.Surface;
        int w = 50;
        int h = 20;
        int x0 = (surf.Width - w) / 2;
        int y0 = (surf.Height - h) / 2;
        var bg = new Color(10, 10, 10, 220);
        var fg = Color.White;

        for (int yy = y0; yy < y0 + h; yy++)
            for (int xx = x0; xx < x0 + w; xx++)
                surf.SetGlyph(xx, yy, ' ', fg, bg);

        DrawBox(surface, x0, y0, w, h, Color.Yellow, bg);

        surf.Print(x0 + 2, y0, $" ZONE: {detail.DisplayName} ", Color.Cyan);

        int line = y0 + 2;
        surf.Print(x0 + 2, line++, $"ID: {detail.ZoneId}", fg);
        surf.Print(x0 + 2, line++, $"Name: {detail.Name}", fg);
        surf.Print(x0 + 2, line++, $"Type: {detail.DisplayName}", fg);
        surf.Print(x0 + 2, line++, $"Category: {detail.Category}", Color.Gray);
        line++;

        surf.Print(x0 + 2, line++, $"Total Cells: {detail.TotalCells}", fg);
        surf.Print(x0 + 2, line++, $"Member Chunks: {detail.MemberChunkCount}", fg);
        surf.Print(x0 + 2, line++, $"Enabled: {(detail.Enabled ? "Yes" : "No")}", detail.Enabled ? Color.Green : Color.Red);
        line++;

        surf.Print(x0 + 2, line++, "--- Settings (Placeholder) ---", Color.Yellow);
        surf.Print(x0 + 2, line++, "[TODO] Zone-specific settings", Color.DarkGray);
        surf.Print(x0 + 2, line++, "[TODO] Priority adjustment", Color.DarkGray);
        surf.Print(x0 + 2, line++, "[TODO] Enable/Disable toggle", Color.DarkGray);

        surf.Print(x0 + 2, y0 + h - 2, "Press ESC to close", Color.Gray);
    }
}
