using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen;
using SadConsole;
using SadRogue.Primitives;
using TerrainKind = HumanFortress.Simulation.Tiles.TerrainKind;

namespace HumanFortress.App.Rendering;

internal static class FortressTilePopupRenderer
{
    public static void Render(
        ScreenSurface overlay,
        FortressMap? fortressMap,
        World? world,
        Point tileWorldPosition,
        int tileZ,
        IRuntimeGeologyCatalog? geologyCatalog)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        if (fortressMap == null || world == null)
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
        surf.Print(x0 + 2, y0 + 1, $"Pos: ({tileWorldPosition.X},{tileWorldPosition.Y},{tileZ})", Color.White);

        int chunkX = tileWorldPosition.X / 32;
        int chunkY = tileWorldPosition.Y / 32;
        int localX = tileWorldPosition.X % 32;
        int localY = tileWorldPosition.Y % 32;

        var key = new ChunkKey(chunkX, chunkY, tileZ);
        var simChunk = world.GetChunk(key);
        if (simChunk != null)
        {
            var tile = simChunk.GetTile(localX, localY);
            var geology = geologyCatalog?.GetGeologyByHandle(tile.GeoMatId);
            string geoId = geology?.Id ?? $"#${tile.GeoMatId}";

            int line = 3;

            surf.Print(x0 + 2, line++, "--- Terrain ---", Color.Yellow);
            surf.Print(x0 + 2, line++, $"Kind: {tile.Kind}", tile.Kind == TerrainKind.OpenWithFloor ? Color.Green : Color.White);
            surf.Print(x0 + 2, line++, $"Geology: {geoId.Replace("core_geology_", "").Replace("core_terrain_", "")}", Color.Gray);
            surf.Print(x0 + 2, line++, $"Natural: {tile.IsNatural}  Modifiable: {tile.IsModifiable}", Color.DarkGray);
            line++;

            surf.Print(x0 + 2, line++, "--- Surface ---", Color.Yellow);
            surf.Print(x0 + 2, line++, $"Mud: {tile.HasMud}  Grass: {tile.HasGrass}  Snow: {tile.HasSnow}", Color.Gray);
            surf.Print(x0 + 2, line++, $"Fertility: {tile.Fertility}", Color.DarkGray);
            line++;

            DrawItems(surf, world, tileWorldPosition, tileZ, x0 + 2, ref line);
            line++;

            DrawCreatures(surf, world, tileWorldPosition, tileZ, x0 + 2, ref line);
            line++;

            surf.Print(x0 + 2, line++, "--- Fluids ---", Color.Yellow);
            surf.Print(x0 + 2, line++, $"Kind: {tile.FluidKind}  Depth: {tile.FluidDepth}", Color.Gray);
        }

        surf.Print(x0 + 2, y0 + h - 1, "ESC to close", Color.DarkGray);
    }

    private static void DrawItems(ICellSurface surf, World world, Point tileWorldPosition, int tileZ, int x, ref int line)
    {
        surf.Print(x, line++, "--- Items ---", Color.Yellow);
        var items = world.Items.GetGroundItemsAt(tileWorldPosition, tileZ)
            .ToList();

        if (items.Count > 0)
        {
            foreach (var item in items.Take(5))
            {
                var def = world.Items.GetDefinition(item.DefinitionId);
                string itemName = def?.Name ?? item.DefinitionId;
                surf.Print(x, line++, $"  {itemName} x{item.StackCount}", Color.LightGreen);
            }

            if (items.Count > 5)
                surf.Print(x, line++, $"  ... +{items.Count - 5} more", Color.DarkGray);
        }
        else
        {
            surf.Print(x, line++, "  (none)", Color.DarkGray);
        }
    }

    private static void DrawCreatures(ICellSurface surf, World world, Point tileWorldPosition, int tileZ, int x, ref int line)
    {
        surf.Print(x, line++, "--- Creatures ---", Color.Yellow);
        var creatures = world.Creatures.GetAllInstances()
            .Where(c => c.Position.X == tileWorldPosition.X && c.Position.Y == tileWorldPosition.Y && c.Z == tileZ)
            .ToList();

        if (creatures.Count > 0)
        {
            foreach (var creature in creatures.Take(3))
            {
                var def = world.Creatures.GetDefinition(creature.DefinitionId);
                string name = def?.Name ?? creature.DefinitionId;
                surf.Print(x, line++, $"  {name} HP:{creature.HP}/{creature.MaxHP}", Color.LightBlue);
            }

            if (creatures.Count > 3)
                surf.Print(x, line++, $"  ... +{creatures.Count - 3} more", Color.DarkGray);
        }
        else
        {
            surf.Print(x, line++, "  (none)", Color.DarkGray);
        }
    }
}
