using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private static void RenderWorkshopListColumn(ICellSurface surf, Rectangle area, SimulationWorkshopDebugData workshops)
    {
        surf.Print(area.X + 1, area.Y, "Workshops", Color.Yellow);
        if (workshops.Workshops.Count == 0)
        {
            surf.Print(area.X + 1, area.Y + 2, "No workshops constructed yet.", Color.DarkGray);
            surf.Print(area.X + 1, area.Y + 4, "Build a Stoneworks to cut", Color.Gray);
            surf.Print(area.X + 1, area.Y + 5, "boulders into blocks.", Color.Gray);
            return;
        }

        int line = area.Y + 2;
        int maxLine = area.Y + area.Height - 2;
        foreach (var ws in workshops.Workshops)
        {
            if (line >= maxLine) break;
            string status = ws.IsSite ? "Site" : $"Workers {ws.ActiveJobs}/{ws.AllowedWorkers}";
            if (!ws.IsSite)
            {
                status += $"  Queue {ws.QueueCount}";
            }
            Color fg = ws.IsSite ? Color.DarkGray : (ws.HasBlockedQueue ? Color.Orange : Color.White);
            surf.Print(area.X + 1, line++, $"{ws.Name}", fg);
            if (line >= maxLine) break;
            surf.Print(area.X + 3, line++, $"Pos ({ws.X},{ws.Y},{ws.Z}) [{status}]", Color.Gray);
        }

        if (workshops.Workshops.Count > (maxLine - (area.Y + 2)) / 2)
        {
            surf.Print(area.X + 1, maxLine, $"... {workshops.Workshops.Count - (maxLine - (area.Y + 2)) / 2} more", Color.DarkGray);
        }
    }
}
