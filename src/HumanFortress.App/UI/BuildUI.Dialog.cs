using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class BuildUI
{
    public void DrawConstructionMaterialDialog(ScreenSurface surface, UiStore ui)
    {
        if (!ui.ConstructionMaterialDialogOpen) return;
        var surf = surface.Surface;
        int w = 36, h = 8;
        int x0 = (surf.Width - w) / 2;
        int y0 = (surf.Height - h) / 2;
        var bg = new Color(0, 0, 0, 220);
        var fg = Color.White;

        FillDialog(surf, x0, y0, w, h, fg, bg);
        surf.Print(x0 + 2, y0, " MATERIALS ", Color.Yellow);
        DrawMaterialOptions(surf, ui.SelectedConstructionShape, x0, y0, h, fg);
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

    private static void DrawMaterialOptions(ICellSurface surf, UiConstructionShape shape, int x0, int y0, int height, Color fg)
    {
        switch (shape)
        {
            case UiConstructionShape.Wall:
                surf.Print(x0 + 2, y0 + 2, "[Z] Stone Block", fg);
                surf.Print(x0 + 2, y0 + 3, "[X] Wood Log", fg);
                break;
            case UiConstructionShape.Floor:
                surf.Print(x0 + 2, y0 + 2, "[Z] Stone Block", fg);
                surf.Print(x0 + 2, y0 + 3, "[X] Wood Plank", fg);
                break;
            case UiConstructionShape.Ramp:
                surf.Print(x0 + 2, y0 + 2, "Ramp requires both:", fg);
                surf.Print(x0 + 2, y0 + 3, "[ENTER] Confirm Stone+Plank", fg);
                break;
            default:
                surf.Print(x0 + 2, y0 + 2, "(No options)", Color.Gray);
                break;
        }

        surf.Print(x0 + 2, y0 + height - 2, "ESC: Cancel", Color.Gray);
    }
}
