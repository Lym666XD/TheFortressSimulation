using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;
using System.Linq;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private static void RenderWorkshopDirectory(ICellSurface surf, Rectangle area, SimulationWorkshopDebugData workshops)
    {
        surf.Print(area.X + 1, area.Y, $"Workshops ({workshops.Workshops.Count})", Color.Yellow);
        int line = area.Y + 2;
        foreach (var ws in workshops.Workshops.Take(area.Height - 2))
        {
            string label = $"{ws.Name,-18} ({ws.X,3},{ws.Y,3},{ws.Z,2})";
            Color color = ws.IsSite ? Color.Orange : Color.White;
            surf.Print(area.X + 1, line++, label, color);
        }
        if (workshops.Workshops.Count == 0)
        {
            surf.Print(area.X + 1, line, "No workshops placed yet.", Color.DarkGray);
        }
    }
}
