using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;
using System.Linq;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private readonly record struct ActiveJobRow(string Kind, string Worker, string Stage, string Target, Color Color);

    private static void RenderOrdersSummaryColumn(ICellSurface surf, Rectangle area, SimulationWorkDrawerData work)
    {
        var orders = work.Orders;

        surf.Print(area.X + 1, area.Y, "Order Summary", Color.Yellow);
        int line = area.Y + 2;
        surf.Print(area.X + 1, line++, $"Haul designations: {orders.ActiveHaulDesignations}", Color.Cyan);
        surf.Print(area.X + 1, line++, $"Mining designations: {orders.ActiveMiningDesignations}", Color.Cyan);
        surf.Print(area.X + 1, line++, $"Construction sites: {orders.ActiveConstructionSites}", Color.Cyan);
        line++;
        surf.Print(area.X + 1, line++, "Hints:", Color.Yellow);
        surf.Print(area.X + 2, line++, "- Use Z menu to add jobs", Color.Gray);
        surf.Print(area.X + 2, line++, "- Shift-click cancels orders", Color.Gray);
    }

    private static void RenderJobsColumn(ICellSurface surf, Rectangle area, SimulationWorkDrawerData work)
    {
        var rows = BuildActiveJobRows(work.Jobs);
        var orders = work.Orders;
        surf.Print(area.X + 1, area.Y, "Active Jobs", Color.Yellow);
        surf.Print(area.X + 1, area.Y + 1, "Type  Worker   Stage          Target", Color.Gray);
        int line = area.Y + 2;
        int maxRows = area.Height - 6;
        foreach (var row in rows.Take(maxRows))
        {
            surf.Print(area.X + 1, line++, $"{row.Kind,-5}{row.Worker,-9}{Truncate(row.Stage, 14),-14}{row.Target}", row.Color);
        }

        if (rows.Count == 0)
        {
            surf.Print(area.X + 1, line++, "No active jobs. Use Orders menu to queue work.", Color.DarkGray);
        }

        line += 1;
        surf.Print(area.X + 1, line++, "Recent designations:", Color.Yellow);
        foreach (var designation in orders.RecentDesignations)
        {
            var color = string.Equals(designation.Kind, "Mine", StringComparison.OrdinalIgnoreCase)
                ? Color.LightGreen
                : Color.Cyan;
            surf.Print(area.X + 2, line++, $"[{designation.Kind}] {designation.Description}", color);
        }

        if (orders.RecentDesignations.Count == 0)
        {
            surf.Print(area.X + 2, line++, "(No recent orders)", Color.DarkGray);
        }
    }

    private static List<ActiveJobRow> BuildActiveJobRows(SimulationJobsDebugData? jobs)
    {
        var rows = new List<ActiveJobRow>();
        if (!jobs.HasValue)
            return rows;

        foreach (var job in jobs.Value.ActiveJobs)
        {
            var worker = job.WorkerId.ToString("N").Substring(0, 6);
            rows.Add(new ActiveJobRow(job.Kind, worker, job.Stage, job.Target, GetActiveJobColor(job.Kind)));
        }

        return rows;
    }

    private static Color GetActiveJobColor(string kind)
    {
        return kind switch
        {
            "Haul" => Color.Cyan,
            "Mine" => Color.LightGreen,
            "Craft" => new Color(200, 160, 255),
            _ => Color.White
        };
    }
}
