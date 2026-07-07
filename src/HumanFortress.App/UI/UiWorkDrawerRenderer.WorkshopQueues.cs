using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private static void RenderWorkshopNotesColumn(ICellSurface surf, Rectangle area, SimulationWorkshopDebugData workshops)
    {
        surf.Print(area.X + 1, area.Y, "Active Queues", Color.Yellow);
        int line = area.Y + 2;
        if (workshops.Workshops.Count == 0)
        {
            surf.Print(area.X + 1, line, "No workshops online.", Color.DarkGray);
            return;
        }

        foreach (var ws in workshops.Workshops)
        {
            if (line >= area.Y + area.Height - 2) break;
            surf.Print(area.X + 1, line++, ws.Name, ws.HasBlockedQueue ? Color.Orange : Color.White);
            if (ws.Queue.Count == 0)
            {
                if (line >= area.Y + area.Height - 2) break;
                surf.Print(area.X + 2, line++, "No queued recipes.", Color.DarkGray);
                continue;
            }

            foreach (var entry in ws.Queue)
            {
                if (line >= area.Y + area.Height - 2) break;
                surf.Print(area.X + 2, line++, $"{entry.Prefix} {entry.DisplayName} - {entry.StatusText}", entry.IsBlocked ? Color.Orange : Color.Gray);
            }
        }
    }
}
