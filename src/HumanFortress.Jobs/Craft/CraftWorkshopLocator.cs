using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.Placeables;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Craft;

internal sealed class CraftWorkshopLocator
{
    private readonly WorldModel _world;
    private readonly IConstructionCatalog _constructions;

    public CraftWorkshopLocator(
        WorldModel world,
        IConstructionCatalog constructions)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
    }

    public bool TryFind(Guid workshopGuid, out PlaceableInstance? placeable, out WorkshopState? state)
    {
        placeable = null;
        state = null;
        foreach (var chunk in _world.GetAllChunks())
        {
            var placeables = chunk.GetPlaceableData();
            if (placeables == null)
            {
                continue;
            }

            foreach (var candidate in placeables.GetAllOwnedPlaceables())
            {
                if (candidate.Guid != workshopGuid)
                {
                    continue;
                }

                placeable = candidate;
                state = candidate.Workshop;
                return state != null;
            }
        }

        return false;
    }

    public IEnumerable<(PlaceableInstance Placeable, ConstructionDefinition? Definition)> EnumerateWorkshops()
    {
        var list = new List<(PlaceableInstance, ConstructionDefinition?)>();
        foreach (var chunk in _world.GetAllChunks())
        {
            var placeables = chunk.GetPlaceableData();
            if (placeables == null)
            {
                continue;
            }

            foreach (var placeable in placeables.GetAllOwnedPlaceables())
            {
                if (placeable.Workshop == null)
                {
                    continue;
                }

                var definition = _constructions.GetConstruction(placeable.DefinitionId);
                list.Add((placeable, definition));
            }
        }

        return list
            .OrderBy(t => t.Item1.Z)
            .ThenBy(t => t.Item1.Position.Y)
            .ThenBy(t => t.Item1.Position.X);
    }

    public static bool IsOnFootprint(PlaceableInstance placeable, int x, int y, int z)
    {
        return z == placeable.Z && IsOnFootprint(placeable, x, y);
    }

    public static bool IsOnFootprint(PlaceableInstance placeable, int x, int y)
    {
        var footprint = placeable.Footprint;
        return x >= placeable.Position.X && x < placeable.Position.X + footprint.W
               && y >= placeable.Position.Y && y < placeable.Position.Y + footprint.D;
    }

    public static IEnumerable<(int X, int Y)> EnumerateFootprintAndRing(PlaceableInstance placeable)
    {
        var seen = new HashSet<(int, int)>();
        var footprint = placeable.Footprint;
        for (int dy = 0; dy < footprint.D; dy++)
        for (int dx = 0; dx < footprint.W; dx++)
        {
            int x = placeable.Position.X + dx;
            int y = placeable.Position.Y + dy;
            if (seen.Add((x, y)))
            {
                yield return (x, y);
            }
        }

        var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        for (int dy = 0; dy < footprint.D; dy++)
        for (int dx = 0; dx < footprint.W; dx++)
        {
            int baseX = placeable.Position.X + dx;
            int baseY = placeable.Position.Y + dy;
            foreach (var (offsetX, offsetY) in dirs)
            {
                int nextX = baseX + offsetX;
                int nextY = baseY + offsetY;
                if (seen.Add((nextX, nextY)))
                {
                    yield return (nextX, nextY);
                }
            }
        }
    }
}
