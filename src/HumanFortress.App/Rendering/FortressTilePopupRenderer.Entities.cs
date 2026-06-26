using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal static partial class FortressTilePopupRenderer
{
    private static void DrawItems(ICellSurface surf, IReadOnlyList<TileInspectionItemView> items, int x, ref int line)
    {
        surf.Print(x, line++, "--- Items ---", Color.Yellow);
        if (items.Count > 0)
        {
            foreach (var item in items.Take(5))
                surf.Print(x, line++, $"  {item.DisplayName} x{item.StackCount}", Color.LightGreen);

            if (items.Count > 5)
                surf.Print(x, line++, $"  ... +{items.Count - 5} more", Color.DarkGray);
        }
        else
        {
            surf.Print(x, line++, "  (none)", Color.DarkGray);
        }
    }

    private static void DrawCreatures(ICellSurface surf, IReadOnlyList<TileInspectionCreatureView> creatures, int x, ref int line)
    {
        surf.Print(x, line++, "--- Creatures ---", Color.Yellow);
        if (creatures.Count > 0)
        {
            foreach (var creature in creatures.Take(3))
                surf.Print(x, line++, $"  {creature.DisplayName} HP:{creature.HP}/{creature.MaxHP}", Color.LightBlue);

            if (creatures.Count > 3)
                surf.Print(x, line++, $"  ... +{creatures.Count - 3} more", Color.DarkGray);
        }
        else
        {
            surf.Print(x, line++, "  (none)", Color.DarkGray);
        }
    }
}
