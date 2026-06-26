using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static class UiWorkshopPanelRenderer
{
    public static void Draw(
        ScreenSurface surface,
        UiStore ui,
        SimulationWorkshopDebugData workshops)
    {
        if (!ui.WorkshopPanelOpen || ui.OpenWorkshopGuid == null) return;

        var workshop = workshops.Workshops
            .Where(candidate => candidate.WorkshopGuid == ui.OpenWorkshopGuid.Value)
            .Select(candidate => (WorkshopSummaryView?)candidate)
            .FirstOrDefault();
        if (!workshop.HasValue)
            return;

        var value = workshop.Value;

        var surf = surface.Surface;
        int w = 56, h = 16;
        int x0 = (surf.Width - w) / 2;
        int y0 = (surf.Height - h) / 2;
        var bg = Color.Black.SetAlpha(220);
        var fg = Color.White;
        var hi = Color.Cyan;
        for (int y = y0; y < y0 + h; y++)
        for (int x = x0; x < x0 + w; x++)
            surf.SetGlyph(x, y, ' ', fg, bg);
        for (int x = x0; x < x0 + w; x++)
        {
            surf.SetGlyph(x, y0, '-');
            surf.SetGlyph(x, y0 + h - 1, '-');
        }
        for (int y = y0; y < y0 + h; y++)
        {
            surf.SetGlyph(x0, y, '|');
            surf.SetGlyph(x0 + w - 1, y, '|');
        }
        surf.SetGlyph(x0, y0, '+'); surf.SetGlyph(x0 + w - 1, y0, '+');
        surf.SetGlyph(x0, y0 + h - 1, '+'); surf.SetGlyph(x0 + w - 1, y0 + h - 1, '+');

        surf.Print(x0 + 2, y0, $" {value.Name} ", hi);
        surf.Print(x0 + 2, y0 + 2, $"Id: {value.DefinitionId}", fg);
        surf.Print(x0 + 2, y0 + 3, $"Pos: ({value.X},{value.Y},{value.Z})", fg);
        surf.Print(x0 + 2, y0 + 4, $"Footprint: {value.FootprintW}x{value.FootprintD}  Pass: {value.Passability}", fg);
        surf.Print(x0 + 2, y0 + 5, $"Tags: [{string.Join(',', value.Tags)}]", Color.Gray);

        surf.Print(x0 + 2, y0 + 7, $"Workers {value.ActiveJobs}/{value.AllowedWorkers} (Max {value.MaxWorkers})  [+]/[-]", Color.White);
        surf.Print(x0 + 2, y0 + 8, $"Supply: {(value.AutoRequestMaterials ? "Auto" : "Manual")} (S)   Stockpile: {(value.AutoStockpileOutputs ? "Auto" : "Manual")} (O)", Color.Gray);
        surf.Print(x0 + 2, y0 + 9, $"Attachment slots: {value.AttachmentSlotCount}", Color.DarkGray);

        surf.Print(x0 + 2, y0 + 11, "Queue [A:Add, Delete=Remove, PgUp/PgDn=Move]", Color.Yellow);
        int queueStart = y0 + 12;
        int maxRows = h - 4;
        int selected = Math.Clamp(ui.WorkshopQueueSelectedIndex, 0, Math.Max(0, value.Queue.Count - 1));
        if (value.Queue.Count == 0)
        {
            surf.Print(x0 + 2, queueStart, "Queue empty. Press A to add the default recipe.", Color.DarkGray);
        }
        else
        {
            int row = 0;
            foreach (var entry in value.Queue)
            {
                if (row >= maxRows) break;
                int y = queueStart + row;
                bool isSelected = row == selected;
                if (isSelected)
                {
                    for (int cx = x0 + 1; cx < x0 + w - 1; cx++)
                        surf.SetGlyph(cx, y, ' ', Color.White, new Color(30, 30, 10));
                }
                var color = entry.IsBlocked ? Color.Orange : Color.White;
                surf.Print(x0 + 2, y, $"{entry.Prefix} {entry.DisplayName} - {entry.StatusText}", color);
                row++;
            }
        }

        surf.Print(x0 + 2, y0 + h - 2, "ESC/Right-click: close", Color.DarkGray);
        surf.Print(x0 + w - 18, y0 + h - 2, $"#{value.WorkshopGuid.ToString()[..8]}", Color.DarkGray);
    }
}
