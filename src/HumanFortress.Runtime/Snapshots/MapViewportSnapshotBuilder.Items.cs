using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class MapViewportSnapshotBuilder
{
    private static (int Glyph, Color Color) GetItemDisplay(World world, ItemInstance item)
    {
        var definition = world.Items.GetDefinition(item.DefinitionId);
        if (definition == null)
            return ('?', Color.Gray);

        var kind = definition.Kind.ToLowerInvariant();
        int glyph = kind switch
        {
            "resource" => '*',
            "weapon" => '/',
            "armor" => '[',
            "tool" => '&',
            "container" => 'U',
            "consumable" => '%',
            _ => '?',
        };

        var color = kind switch
        {
            "resource" => Color.Brown,
            "weapon" => Color.Silver,
            "armor" => Color.LightGray,
            "tool" => Color.Yellow,
            "container" => Color.DarkGoldenrod,
            "consumable" => Color.Green,
            _ => Color.Gray,
        };

        return (glyph, color);
    }
}
