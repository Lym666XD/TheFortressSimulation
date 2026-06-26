using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class MapViewportSnapshotBuilder
{
    private static (int Glyph, Color Color) GetCreatureDisplay(World world, CreatureInstance creature)
    {
        var definition = world.Creatures.GetDefinition(creature.DefinitionId);
        if (definition == null)
            return ('@', Color.White);

        int glyph = definition.Name.Length > 0 ? char.ToUpperInvariant(definition.Name[0]) : '@';
        var color = Color.White;

        if (definition.Tags.Contains("civilized"))
            color = Color.Cyan;
        else if (definition.Tags.Contains("hostile"))
            color = Color.Red;
        else if (definition.Tags.Contains("wildlife"))
            color = Color.Green;

        return (glyph, color);
    }
}
