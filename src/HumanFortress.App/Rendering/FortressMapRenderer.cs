using HumanFortress.App.UI;
using HumanFortress.Core.Content;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen;
using SadConsole;
using SadRogue.Primitives;
using TerrainKind = HumanFortress.Simulation.Tiles.TerrainKind;

namespace HumanFortress.App.Rendering;

internal static class FortressMapRenderer
{
    public static void Render(
        MapScreenSurface? mapSurface,
        FortressMap? fortressMap,
        World? world,
        int fortressSize,
        Point cameraPosition,
        Point cursorPosition,
        int currentZ,
        int zoomLevel,
        UiContext uiContext,
        NavigationOverlay? navigationOverlay)
    {
        try
        {
            if (mapSurface == null)
                return;

            mapSurface.Clear();

            if (fortressMap == null)
            {
                System.Console.WriteLine("[RenderMap] WARNING: FortressMap is null");
                return;
            }

            int maxWorldSize = fortressSize * 32;
            int viewW = mapSurface.Surface.Width;
            int viewH = mapSurface.Surface.Height;

            RenderTerrain(mapSurface, world, maxWorldSize, cameraPosition, cursorPosition, currentZ, zoomLevel, uiContext, viewW, viewH);

            if (world != null)
            {
                RenderEntities(mapSurface, world, cameraPosition, currentZ, viewW, viewH);
            }

            if (navigationOverlay != null && world != null)
            {
                var viewport = new Rectangle(cameraPosition.X, cameraPosition.Y, viewW, viewH);
                navigationOverlay.RenderOverlay(mapSurface, world, currentZ, viewport);
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[RenderMap] ERROR: {ex.Message}");
            System.Console.WriteLine($"[RenderMap] Stack trace: {ex.StackTrace}");
        }
    }

    private static void RenderTerrain(
        MapScreenSurface mapSurface,
        World? world,
        int maxWorldSize,
        Point cameraPosition,
        Point cursorPosition,
        int currentZ,
        int zoomLevel,
        UiContext uiContext,
        int viewW,
        int viewH)
    {
        for (int sx = 0; sx < viewW; sx++)
        {
            for (int sy = 0; sy < viewH; sy++)
            {
                int worldX = cameraPosition.X + (sx / zoomLevel);
                int worldY = cameraPosition.Y + (sy / zoomLevel);

                if (worldX < 0 || worldX >= maxWorldSize ||
                    worldY < 0 || worldY >= maxWorldSize)
                {
                    mapSurface.SetGlyph(sx, sy, '#', Color.DarkGray, Color.Transparent);
                    continue;
                }

                if (world == null)
                {
                    mapSurface.SetGlyph(sx, sy, '?', Color.DarkGray, Color.Transparent);
                    continue;
                }

                var (glyph, color) = GetWorldTileDisplay(world, worldX, worldY, currentZ);
                DrawTerrainCell(mapSurface, sx, sy, worldX, worldY, cursorPosition, uiContext, zoomLevel, viewW, viewH, glyph, color);
            }
        }
    }

    private static (int glyph, Color color) GetWorldTileDisplay(World world, int worldX, int worldY, int currentZ)
    {
        int currentChunkX = worldX / 32;
        int currentChunkY = worldY / 32;
        int localX = worldX % 32;
        int localY = worldY % 32;

        var chunkKey = new ChunkKey(currentChunkX, currentChunkY, currentZ);
        var simChunk = world.GetChunk(chunkKey);
        if (simChunk == null)
            return ('#', Color.DarkGray);

        var tile = simChunk.GetTile(localX, localY);
        return GetTileDisplay(tile);
    }

    private static void DrawTerrainCell(
        MapScreenSurface mapSurface,
        int screenX,
        int screenY,
        int worldX,
        int worldY,
        Point cursorPosition,
        UiContext uiContext,
        int zoomLevel,
        int viewW,
        int viewH,
        int glyph,
        Color color)
    {
        bool isCursor = worldX == cursorPosition.X && worldY == cursorPosition.Y;
        var cursorGlyph = uiContext == UiContext.Global ? 'X' : '.';

        if (isCursor && zoomLevel == 1)
        {
            mapSurface.SetGlyph(screenX, screenY, cursorGlyph, Color.Yellow, Color.Transparent);
            return;
        }

        if (zoomLevel > 1)
        {
            for (int zx = 0; zx < zoomLevel && screenX + zx < viewW; zx++)
            {
                for (int zy = 0; zy < zoomLevel && screenY + zy < viewH; zy++)
                {
                    mapSurface.SetGlyph(
                        screenX + zx,
                        screenY + zy,
                        isCursor ? cursorGlyph : glyph,
                        isCursor ? Color.Yellow : color,
                        Color.Transparent);
                }
            }

            return;
        }

        mapSurface.SetGlyph(screenX, screenY, glyph, color, Color.Transparent);
    }

    private static (int glyph, Color color) GetTileDisplay(TileBase tile)
    {
        var geology = ContentRegistry.Instance.GetGeologyByHandle(tile.GeoMatId);
        var fg = geology != null
            ? new Color(geology.Display.Foreground.R, geology.Display.Foreground.G, geology.Display.Foreground.B)
            : Color.Gray;

        int glyph;
        switch (tile.Kind)
        {
            case TerrainKind.SolidWall:
                glyph = geology?.Display.Glyph ?? '#';
                break;
            case TerrainKind.OpenWithFloor:
                if (tile.HasSnow)
                {
                    glyph = '*';
                    fg = Color.White;
                }
                else if (tile.HasGrass)
                {
                    glyph = ',';
                    fg = Color.Green;
                }
                else if (tile.HasMud)
                {
                    glyph = '~';
                    fg = Color.DarkGoldenrod;
                }
                else if (tile.HasMoss)
                {
                    glyph = ',';
                    fg = new Color(0, 120, 0);
                }
                else
                {
                    glyph = '.';
                }
                break;
            case TerrainKind.OpenNoFloor:
                glyph = ' ';
                break;
            case TerrainKind.Ramp:
                glyph = '^';
                break;
            case TerrainKind.StairsUp:
                glyph = '<';
                break;
            case TerrainKind.StairsDown:
                glyph = '>';
                break;
            case TerrainKind.StairsUD:
                glyph = 'X';
                break;
            default:
                glyph = geology?.Display.Glyph ?? '?';
                break;
        }

        return (glyph, fg);
    }

    private static void RenderEntities(MapScreenSurface mapSurface, World world, Point cameraPosition, int currentZ, int viewW, int viewH)
    {
        var creatures = world.Creatures.GetAllInstances()
            .Where(c => c.Z == currentZ && c.HP > 0)
            .ToList();
        var creaturePositions = new HashSet<Point>();

        foreach (var creature in creatures)
        {
            creaturePositions.Add(creature.Position);

            int screenX = creature.Position.X - cameraPosition.X;
            int screenY = creature.Position.Y - cameraPosition.Y;
            if (screenX >= 0 && screenX < viewW && screenY >= 0 && screenY < viewH)
            {
                var (glyph, color) = GetCreatureDisplay(world, creature);
                mapSurface.SetGlyph(screenX, screenY, glyph, color, Color.Transparent);
            }
        }

        var items = world.Items.GetAllInstances()
            .Where(i => i.Z == currentZ && i.IsOnGround)
            .ToList();

        foreach (var item in items)
        {
            int screenX = item.Position.X - cameraPosition.X;
            int screenY = item.Position.Y - cameraPosition.Y;
            if (screenX < 0 || screenX >= viewW || screenY < 0 || screenY >= viewH)
                continue;

            if (creaturePositions.Contains(item.Position))
                continue;

            var (glyph, color) = GetItemDisplay(world, item);
            mapSurface.SetGlyph(screenX, screenY, glyph, color, Color.Transparent);
        }
    }

    private static (int glyph, Color color) GetCreatureDisplay(World world, CreatureInstance creature)
    {
        var def = world.Creatures.GetDefinition(creature.DefinitionId);

        int glyph = '@';
        Color color = Color.White;

        if (def != null)
        {
            glyph = def.Name.Length > 0 ? char.ToUpperInvariant(def.Name[0]) : '@';

            if (def.Tags.Contains("civilized"))
                color = Color.Cyan;
            else if (def.Tags.Contains("hostile"))
                color = Color.Red;
            else if (def.Tags.Contains("wildlife"))
                color = Color.Green;
        }

        return (glyph, color);
    }

    private static (int glyph, Color color) GetItemDisplay(World world, ItemInstance item)
    {
        var def = world.Items.GetDefinition(item.DefinitionId);

        int glyph = '?';
        Color color = Color.Gray;

        if (def != null)
        {
            var kind = def.Kind.ToLowerInvariant();
            glyph = kind switch
            {
                "resource" => '*',
                "weapon" => '/',
                "armor" => '[',
                "tool" => '&',
                "container" => 'U',
                "consumable" => '%',
                _ => '?'
            };

            color = kind switch
            {
                "resource" => Color.Brown,
                "weapon" => Color.Silver,
                "armor" => Color.LightGray,
                "tool" => Color.Yellow,
                "container" => Color.DarkGoldenrod,
                "consumable" => Color.Green,
                _ => Color.Gray
            };
        }

        return (glyph, color);
    }
}
