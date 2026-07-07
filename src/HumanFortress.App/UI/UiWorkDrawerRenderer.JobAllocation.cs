using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    public static void DrawJobAllocationTab(ICellSurface surf, UiStore ui, int startY, int maxHeight, SimulationWorkDrawerData work)
    {
        var workforce = work.Workforce;
        var defs = workforce.Professions;
        if (defs.Count == 0)
        {
            surf.Print(2, startY + 2, "No professions defined in registry.", Color.Gray);
            return;
        }

        var roster = workforce.Roster;
        int areaHeight = Math.Max(10, maxHeight);
        var area = new Rectangle(1, startY, surf.Width - 2, areaHeight);
        FillArea(surf, area, new Color(15, 15, 15));
        surf.Print(area.X + 1, area.Y, "Job Allocation (click value to cycle 1-9 / '-')", Color.Yellow);

        if (roster.Count == 0)
        {
            surf.Print(area.X + 1, area.Y + 2, "No dwarves available.", Color.DarkGray);
            return;
        }

        int nameWidth = Math.Max(12, area.Width / 6);
        int tableWidth = Math.Max(8, area.Width - nameWidth - 3);
        int colWidth = Math.Max(3, tableWidth / defs.Count);
        int headerY = area.Y + 1;
        int nameX = area.X + 1;
        surf.Print(nameX, headerY, "Worker".PadRight(nameWidth - 1), Color.Gray);

        for (int col = 0; col < defs.Count; col++)
        {
            int colX = nameX + nameWidth + col * colWidth;
            string label = defs[col].Name.ToUpperInvariant();
            if (label.Length > colWidth - 1)
                label = label.Substring(0, Math.Max(1, colWidth - 1));
            var color = col == ui.WorkAllocSelectedCol ? Color.LightCyan : Color.DarkGray;
            surf.Print(colX, headerY, label, color);
        }

        int visibleRows = Math.Max(1, area.Height - 4);
        ui.WorkAllocSelectedRow = Math.Clamp(ui.WorkAllocSelectedRow, 0, roster.Count - 1);
        ui.WorkAllocSelectedCol = Math.Clamp(ui.WorkAllocSelectedCol, 0, defs.Count - 1);
        int maxOffset = Math.Max(0, roster.Count - visibleRows);
        ui.WorkAllocRowOffset = Math.Clamp(ui.WorkAllocRowOffset, 0, maxOffset);
        if (ui.WorkAllocSelectedRow < ui.WorkAllocRowOffset)
            ui.WorkAllocRowOffset = ui.WorkAllocSelectedRow;
        if (ui.WorkAllocSelectedRow >= ui.WorkAllocRowOffset + visibleRows)
            ui.WorkAllocRowOffset = ui.WorkAllocSelectedRow - visibleRows + 1;

        for (int row = 0; row < visibleRows; row++)
        {
            int actual = ui.WorkAllocRowOffset + row;
            if (actual >= roster.Count) break;
            var entry = roster[actual];
            string name = Truncate(entry.Name, nameWidth - 1);
            int rowY = headerY + 1 + row;
            surf.Print(nameX, rowY, name.PadRight(nameWidth - 1), Color.White);

            for (int col = 0; col < defs.Count; col++)
            {
                int colX = nameX + nameWidth + col * colWidth;
                string profId = defs[col].Id;
                int weight = entry.Weights.TryGetValue(profId, out var val) ? val : 0;
                string text = weight <= 0 ? "--" : weight.ToString();
                bool selected = actual == ui.WorkAllocSelectedRow && col == ui.WorkAllocSelectedCol;
                var bg = selected ? new Color(60, 60, 0) : new Color(30, 30, 30);
                for (int i = 0; i < colWidth - 1; i++)
                    surf.SetGlyph(colX + i, rowY, ' ', Color.Black, bg);
                surf.Print(colX, rowY, text, weight <= 0 ? Color.DarkGray : Color.White);
            }
        }

        surf.Print(area.X + 1, area.Y + area.Height - 2, "Use arrows to move, click cell to cycle 1-9 / '-'", Color.DarkGray);
    }
}
