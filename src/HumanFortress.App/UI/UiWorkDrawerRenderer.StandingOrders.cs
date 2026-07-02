using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private static void RenderStandingOrdersColumn(ICellSurface surf, Rectangle area)
    {
        surf.Print(area.X + 1, area.Y, "Standing Orders", Color.Yellow);
        if (area.Height < 4)
            return;

        surf.Print(area.X + 1, area.Y + 2, "No standing orders", Color.Gray);
        if (area.Height >= 6)
        {
            surf.Print(area.X + 1, area.Y + 4, "Workshop queues and stockpile", Color.DarkGray);
            surf.Print(area.X + 1, area.Y + 5, "automation are managed per site.", Color.DarkGray);
        }
    }
}
