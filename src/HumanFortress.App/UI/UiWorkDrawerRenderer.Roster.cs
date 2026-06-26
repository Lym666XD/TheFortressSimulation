using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private static void RenderDwarfRosterColumn(ICellSurface surf, Rectangle area, UiStore ui, SimulationWorkDrawerData work)
    {
        var roster = work.Workforce.Roster;

        surf.Print(area.X + 1, area.Y, "Dwarves On Duty", Color.Yellow);
        surf.Print(area.X + 1, area.Y + 1, "Name         Status", Color.Gray);

        if (roster.Count == 0)
        {
            surf.Print(area.X + 1, area.Y + 3, "No dwarves available.", Color.DarkGray);
            return;
        }

        int rowY = area.Y + 2;
        for (int i = 0; i < roster.Count && rowY < area.Y + area.Height - 2; i++)
        {
            var entry = roster[i];
            string name = entry.Name;
            string status = entry.IsAvailable ? "OK" : "Injured";
            Color statusColor = entry.IsAvailable ? Color.Green : Color.Red;
            if (i == ui.WorkPanelSelectedIndex)
            {
                for (int x = area.X + 1; x < area.X + area.Width - 1; x++)
                    surf.SetGlyph(x, rowY, ' ', Color.Black, new Color(40, 40, 10));
            }
            surf.Print(area.X + 1, rowY, $"{Truncate(name, 18),-18}", Color.White);
            surf.Print(area.X + area.Width - 8, rowY, status, statusColor);
            rowY++;
        }
    }
}
