using HumanFortress.Core.Content;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static class FortressTileInfoPanelRenderer
{
    public static void Render(
        SadConsole.Console? panel,
        FortressMap? fortressMap,
        World? world,
        bool isOpen,
        Point tileWorldPosition,
        int tileZ,
        int fortressSize)
    {
        if (panel == null || fortressMap == null || !isOpen)
            return;

        panel.Clear();
        panel.Print(0, 0, "=== TILE INFO ===", Color.Cyan);

        if (tileWorldPosition.X < 0 ||
            tileWorldPosition.X >= fortressSize * 32 ||
            tileWorldPosition.Y < 0 ||
            tileWorldPosition.Y >= fortressSize * 32)
        {
            panel.Print(0, 2, "Out of bounds", Color.DarkGray);
            return;
        }

        int chunkX = tileWorldPosition.X / 32;
        int chunkY = tileWorldPosition.Y / 32;
        int localX = tileWorldPosition.X % 32;
        int localY = tileWorldPosition.Y % 32;

        var chunk = fortressMap.GetChunk(chunkX, chunkY);
        if (chunk == null)
        {
            panel.Print(0, 2, $"Missing chunk: {chunkX},{chunkY}", Color.DarkGray);
            return;
        }

        var geologyId = chunk.GetGeologyId(localX, localY, tileZ);

        panel.Print(0, 2, $"Position: {tileWorldPosition.X},{tileWorldPosition.Y}", Color.White);
        panel.Print(0, 3, $"Chunk: {chunkX},{chunkY}", Color.Gray);
        panel.Print(0, 4, $"Local: {localX},{localY}", Color.Gray);
        panel.Print(0, 6, $"Terrain: {geologyId}", Color.Green);

        string desc = GetTerrainDescription(geologyId);
        panel.Print(0, 8, "Description:", Color.Yellow);

        int line = DrawWrappedDescription(panel, desc);
        DrawItems(panel, world, tileWorldPosition, tileZ, line);
    }

    private static int DrawWrappedDescription(SadConsole.Console panel, string description)
    {
        var words = description.Split(' ');
        int line = 9;
        int col = 0;
        foreach (var word in words)
        {
            if (col + word.Length > 33)
            {
                line++;
                col = 0;
            }

            if (line < 17)
            {
                panel.Print(col, line, word + " ", Color.DarkGray);
                col += word.Length + 1;
            }
        }

        return line;
    }

    private static void DrawItems(SadConsole.Console panel, World? world, Point tileWorldPosition, int tileZ, int line)
    {
        if (world == null)
            return;

        var itemsHere = world.Items.GetAllInstances()
            .Where(i => i.IsOnGround && i.Position.X == tileWorldPosition.X && i.Position.Y == tileWorldPosition.Y && i.Z == tileZ)
            .ToList();

        if (itemsHere.Count == 0)
            return;

        line = Math.Min(line + 2, 16);
        panel.Print(0, line++, "Items:", Color.Yellow);

        foreach (var grp in itemsHere
            .GroupBy(i => (i.DefinitionId, i.MaterialId ?? string.Empty))
            .OrderBy(g => g.Key.DefinitionId)
            .Take(6))
        {
            var def = world.Items.GetDefinition(grp.Key.DefinitionId);
            string name = def?.Name ?? grp.Key.DefinitionId;
            int qty = grp.Sum(i => i.StackCount);
            if (line >= 17)
                break;

            panel.Print(0, line++, $"{name} x{qty}", Color.White);
        }
    }

    private static string GetTerrainDescription(string geologyId)
    {
        var geology = ContentRegistry.Instance.GetGeology(geologyId);
        var material = geology != null ? ContentRegistry.Instance.GetMaterial(geology.Material) : null;

        if (geology != null && material != null)
        {
            var tags = string.Join(", ", geology.Tags);
            var properties = new List<string>();

            if (geology.Properties.Mineable) properties.Add("mineable");
            if (geology.Properties.Buildable) properties.Add("buildable");
            if (geology.Properties.Smoothable) properties.Add("smoothable");
            if (geology.Properties.Flammable) properties.Add("flammable");

            var durability = material.Struct.Durability;
            var value = material.Valuebaleness;

            return $"{tags}. Durability: {durability:F0}. Value: {value:F1}x. {string.Join(", ", properties)}.";
        }

        return "Unknown terrain type.";
    }
}
