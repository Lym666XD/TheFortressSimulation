using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private readonly record struct WorkCategoryCard(string Name, string Detail, int Active, int Backlog, Color Accent);

    private static void RenderLaborSummaryColumn(ICellSurface surf, Rectangle area, SimulationWorkDrawerData work)
    {
        var jobs = work.Jobs;
        var orders = work.Orders;
        var workforce = work.Workforce;
        var haulStats = jobs?.Transport;
        var miningStats = jobs?.Mining;
        var craftStats = jobs?.Craft;
        var construction = jobs?.Construction ?? default;
        int haulBacklog = haulStats?.Backlog ?? 0;
        int miningBacklog = miningStats?.Backlog ?? 0;
        int craftBacklog = craftStats?.Backlog ?? 0;
        int constructionSites = orders.ActiveConstructionSites;
        int totalDwarves = workforce.AvailableWorkers;
        int busyWorkers = (haulStats?.Active ?? 0) + (miningStats?.Active ?? 0) + (craftStats?.Active ?? 0);
        int idleDwarves = Math.Max(0, totalDwarves - busyWorkers);

        var cards = new List<WorkCategoryCard>
        {
            new("Hauling", $"Backlog {haulBacklog}", haulStats?.Active ?? 0, haulBacklog, new Color(120, 180, 255)),
            new("Mining", $"Backlog {miningBacklog}", miningStats?.Active ?? 0, miningBacklog, new Color(200, 220, 120)),
            new("Construction", $"Sites {constructionSites}", construction.LastIntakeCount, constructionSites, new Color(255, 200, 120)),
            new("Farming", "Crop planner coming soon", 0, 0, new Color(160, 210, 120)),
            new("Crafting", $"Backlog {craftBacklog}", craftStats?.Active ?? 0, craftBacklog, new Color(200, 160, 255)),
            new("Service", $"Idle dwarves {idleDwarves}", idleDwarves, 0, new Color(150, 200, 200))
        };

        int rowY = area.Y + 1;
        surf.Print(area.X + 1, rowY++, "[Labor Overview]", Color.Yellow);
        rowY++;
        foreach (var card in cards)
        {
            if (rowY + 1 >= area.Y + area.Height) break;
            Color bg = new Color(card.Accent.R / 8, card.Accent.G / 8, card.Accent.B / 8);
            for (int y = 0; y < 2; y++)
            {
                for (int x = area.X + 1; x < area.X + area.Width - 1; x++)
                {
                    surf.SetGlyph(x, rowY + y, ' ', Color.Black, bg);
                }
            }
            surf.Print(area.X + 2, rowY, card.Name, Color.White);
            surf.Print(area.X + 2, rowY + 1, card.Detail, Color.Gray);
            surf.Print(area.X + area.Width - 13, rowY, $"Act:{card.Active,3}", card.Accent);
            surf.Print(area.X + area.Width - 13, rowY + 1, $"Back:{card.Backlog,3}", Color.LightGray);
            rowY += 3;
        }

        surf.Print(area.X + 1, area.Y + area.Height - 2, $"Total dwarves: {workforce.TotalWorkers}", Color.Cyan);
    }
}
