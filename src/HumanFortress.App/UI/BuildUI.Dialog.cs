using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class BuildUI
{
    public void DrawConstructionMaterialDialog(
        ScreenSurface surface,
        UiStore ui,
        SimulationBuildCatalogData buildCatalog)
    {
        if (!ui.ConstructionMaterialDialogOpen) return;
        var surf = surface.Surface;
        int w = 36, h = 9;
        int x0 = (surf.Width - w) / 2;
        int y0 = (surf.Height - h) / 2;
        var bg = new Color(0, 0, 0, 220);
        var fg = Color.White;

        FillDialog(surf, x0, y0, w, h, fg, bg);
        surf.Print(x0 + 2, y0, " MATERIALS ", Color.Yellow);
        DrawMaterialOptions(surf, ui.SelectedConstructionShape, buildCatalog, x0, y0, w, h, fg);
    }

    private static void FillDialog(ICellSurface surf, int x0, int y0, int width, int height, Color fg, Color bg)
    {
        for (int yy = y0; yy < y0 + height; yy++)
            for (int xx = x0; xx < x0 + width; xx++)
                surf.SetGlyph(xx, yy, ' ', fg, bg);

        for (int xx = x0; xx < x0 + width; xx++) { surf.SetGlyph(xx, y0, '-'); surf.SetGlyph(xx, y0 + height - 1, '-'); }
        for (int yy = y0; yy < y0 + height; yy++) { surf.SetGlyph(x0, yy, '|'); surf.SetGlyph(x0 + width - 1, yy, '|'); }
        surf.SetGlyph(x0, y0, '+'); surf.SetGlyph(x0 + width - 1, y0, '+'); surf.SetGlyph(x0, y0 + height - 1, '+'); surf.SetGlyph(x0 + width - 1, y0 + height - 1, '+');
    }

    private static void DrawMaterialOptions(
        ICellSurface surf,
        UiConstructionShape shape,
        SimulationBuildCatalogData buildCatalog,
        int x0,
        int y0,
        int width,
        int height,
        Color fg)
    {
        var options = ConstructionMaterialOptionPresentation.GetOptions(buildCatalog, shape);
        if (options.Count == 0)
        {
            surf.Print(x0 + 2, y0 + 2, "(No options)", Color.Gray);
        }
        else
        {
            int visibleOptionCount = Math.Min(options.Count, height - 4);
            for (int index = 0; index < visibleOptionCount; index++)
            {
                string shortcut = ConstructionMaterialOptionPresentation.GetShortcutLabel(index);
                string label = $"[{shortcut}] {options[index].Name}";
                int maxLabelWidth = Math.Max(0, width - 4);
                if (label.Length > maxLabelWidth)
                    label = label[..Math.Max(0, maxLabelWidth - 3)] + "...";

                surf.Print(x0 + 2, y0 + 2 + index, label, fg);
            }
        }

        surf.Print(x0 + 2, y0 + height - 2, "ESC: Cancel", Color.Gray);
    }
}
