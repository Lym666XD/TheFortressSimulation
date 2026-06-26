using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private static void RenderConstructionStatusColumn(ICellSurface surf, Rectangle area, SimulationWorkshopDebugData workshops, SimulationWorkDrawerData work)
    {
        var jobs = work.Jobs;
        var construction = jobs?.Construction ?? default;

        surf.Print(area.X + 1, area.Y, "Construction Status", Color.Yellow);
        int line = area.Y + 2;
        surf.Print(area.X + 1, line++, $"Built workshops: {workshops.BuiltCount}", Color.LightGreen);
        surf.Print(area.X + 1, line++, $"Sites in progress: {workshops.SiteCount}", Color.Orange);
        surf.Print(area.X + 1, line++, $"Queued designations: {workshops.QueuedBuildableDesignations}", Color.Cyan);
        surf.Print(area.X + 1, line++, $"Last tick processed: {construction.LastProcessedSites}", Color.Gray);
        surf.Print(area.X + 1, line++, $"Intake limit: {construction.IntakeLimit}", Color.DarkGray);
    }
}
