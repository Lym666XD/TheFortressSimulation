using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressTilePopupRenderer
{
    public static void Render(
        ScreenSurface overlay,
        SimulationTileInspectionData tile)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        if (!tile.HasTile)
            return;

        var surf = overlay.Surface;
        int w = 42;
        int h = 28;
        int x0 = surf.Width - w - 2;
        int y0 = 2;
        var bg = new Color(10, 10, 10, 220);

        for (int yy = y0; yy < y0 + h; yy++)
            for (int xx = x0; xx < x0 + w; xx++)
                surf.SetGlyph(xx, yy, ' ', Color.White, bg);

        surf.Print(x0 + 2, y0, "=== TILE INFO ===", Color.Cyan);
        surf.Print(x0 + 2, y0 + 1, $"Pos: ({tile.X},{tile.Y},{tile.Z})", Color.White);

        int line = 3;

        surf.Print(x0 + 2, line++, "--- Terrain ---", Color.Yellow);
        surf.Print(x0 + 2, line++, $"Kind: {tile.TerrainKind}", string.Equals(tile.TerrainKind, "OpenWithFloor", StringComparison.Ordinal) ? Color.Green : Color.White);
        surf.Print(x0 + 2, line++, $"Geology: {tile.GeologyLabel}", Color.Gray);
        surf.Print(x0 + 2, line++, $"Natural: {tile.IsNatural}  Modifiable: {tile.IsModifiable}", Color.DarkGray);
        line++;

        surf.Print(x0 + 2, line++, "--- Surface ---", Color.Yellow);
        surf.Print(x0 + 2, line++, $"Mud: {tile.HasMud}  Grass: {tile.HasGrass}  Snow: {tile.HasSnow}", Color.Gray);
        surf.Print(x0 + 2, line++, $"Fertility: {tile.Fertility}", Color.DarkGray);
        line++;

        DrawItems(surf, tile.Items, x0 + 2, ref line);
        line++;

        DrawCreatures(surf, tile.Creatures, x0 + 2, ref line);
        line++;

        surf.Print(x0 + 2, line++, "--- Fluids ---", Color.Yellow);
        surf.Print(x0 + 2, line++, $"Kind: {tile.FluidKind}  Depth: {tile.FluidDepth}", Color.Gray);

        surf.Print(x0 + 2, y0 + h - 1, "ESC to close", Color.DarkGray);
    }
}
