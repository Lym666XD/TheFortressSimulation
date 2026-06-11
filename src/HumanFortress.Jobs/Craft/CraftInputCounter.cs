using HumanFortress.Simulation.Placeables;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

internal sealed class CraftInputCounter
{
    private readonly WorldModel _world;

    public CraftInputCounter(WorldModel world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public Dictionary<string, int> CountAvailableInputs(PlaceableInstance workshop)
    {
        var delivered = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var inputCells = CraftWorkshopLocator.EnumerateFootprintAndRing(workshop).ToHashSet();

        foreach (var item in _world.Items.GetGroundInstances())
        {
            if (item.Z != workshop.Z)
            {
                continue;
            }

            if (!inputCells.Contains((item.Position.X, item.Position.Y)))
            {
                continue;
            }

            delivered[item.DefinitionId] = delivered.GetValueOrDefault(item.DefinitionId, 0) + item.StackCount;
        }

        return delivered;
    }

    public static bool IsInInputArea(PlaceableInstance workshop, int x, int y, int z)
    {
        return z == workshop.Z && CraftWorkshopLocator.EnumerateFootprintAndRing(workshop).Contains((x, y));
    }
}
