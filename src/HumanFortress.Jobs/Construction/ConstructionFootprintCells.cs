using HumanFortress.Simulation.Placeables;
using SadRogue.Primitives;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Construction;

internal sealed class ConstructionFootprintCells
{
    private readonly WorldModel _world;

    internal ConstructionFootprintCells(WorldModel world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    internal IEnumerable<Point> EnumerateFootprintAndRing(PlaceableInstance site)
    {
        var seen = new HashSet<(int X, int Y)>();
        var footprint = site.Footprint;
        for (int dy = 0; dy < footprint.D; dy++)
        for (int dx = 0; dx < footprint.W; dx++)
        {
            int worldX = site.Position.X + dx;
            int worldY = site.Position.Y + dy;
            if (seen.Add((worldX, worldY)))
            {
                yield return new Point(worldX, worldY);
            }
        }

        var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        for (int dy = 0; dy < footprint.D; dy++)
        for (int dx = 0; dx < footprint.W; dx++)
        {
            int worldX = site.Position.X + dx;
            int worldY = site.Position.Y + dy;
            foreach (var (offsetX, offsetY) in dirs)
            {
                int nextX = worldX + offsetX;
                int nextY = worldY + offsetY;
                if (!_world.IsValidPosition(nextX, nextY, site.Z))
                {
                    continue;
                }

                if (seen.Add((nextX, nextY)))
                {
                    yield return new Point(nextX, nextY);
                }
            }
        }
    }
}
