using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiQuickMenuRenderer
{
    private static void DrawWorkshopItemsPane(ScreenSurface surface, int x, int y, string categoryId, SimulationBuildCatalogData? buildCatalog)
    {
        var bg = Color.Black.SetAlpha(200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        DrawWorkshopItemsBox(surface, x, y, fg, bg);
        var category = buildCatalog.HasValue
            ? WorkshopCategoryPresentation.FindCategory(buildCatalog.Value, categoryId)
            : null;
        surface.Print(x + 1, y, $" {category?.DisplayName ?? categoryId} ", highlight);

        var list = category?.Workshops ?? Array.Empty<BuildableConstructionView>();
        var keys = new[] { 'Z', 'X', 'C', 'V', 'F', 'G', 'R', 'T' };
        int max = Math.Min(keys.Length, list.Count);
        for (int i = 0; i < max; i++)
        {
            var d = list[i];
            string size = $"{d.FootprintW}x{d.FootprintD}";
            surface.Print(x + 2, y + 2 + i, $"[{keys[i]}] {d.Name} ({size})", fg);
        }
        if (max == 0)
        {
            surface.Print(x + 2, y + 2, "WIP", Color.Gray);
        }
        surface.Print(x + 2, y + 10, "[,] Back", Color.Gray);
    }

    private static void DrawWorkshopItemsBox(ScreenSurface surface, int x, int y, Color fg, Color bg)
    {
        for (int i = 0; i < 38; i++)
            for (int j = 0; j < 12; j++)
                surface.SetGlyph(x + i, y + j, ' ', fg, bg);
        for (int i = 1; i < 38 - 1; i++)
        {
            surface.SetGlyph(x + i, y, '-');
            surface.SetGlyph(x + i, y + 12 - 1, '-');
        }
        for (int j = 1; j < 12 - 1; j++)
        {
            surface.SetGlyph(x, y + j, '|');
            surface.SetGlyph(x + 38 - 1, y + j, '|');
        }
        surface.SetGlyph(x, y, '+');
        surface.SetGlyph(x + 38 - 1, y, '+');
        surface.SetGlyph(x, y + 12 - 1, '+');
        surface.SetGlyph(x + 38 - 1, y + 12 - 1, '+');
    }

}
