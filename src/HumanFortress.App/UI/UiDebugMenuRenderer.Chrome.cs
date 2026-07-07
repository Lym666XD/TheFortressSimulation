using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiDebugMenuRenderer
{
    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
        return value.Substring(0, Math.Max(0, max - 1)) + "...";
    }

    private static void WritePill(ICellSurface surf, ref int x, int y, string text, Color fg, Color bg)
    {
        surf.SetGlyph(x, y, ' ', Color.White, bg);
        surf.Print(x + 1, y, text, fg);
        x += text.Length + 2;
        surf.SetGlyph(x - 1, y, ' ', Color.White, bg);
    }
}
