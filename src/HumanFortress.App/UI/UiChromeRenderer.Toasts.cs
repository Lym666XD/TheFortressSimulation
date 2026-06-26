using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiChromeRenderer
{
    public static void DrawToasts(ScreenSurface overlay, UiStore ui, ulong tick)
    {
        ui.PruneToasts(tick);
        var surf = overlay.Surface;
        int y = 1;
        foreach (var (text, _) in ui.Toasts)
        {
            surf.Print(2, y++, text, Color.Orange);
            if (y > 6) break;
        }
    }
}
