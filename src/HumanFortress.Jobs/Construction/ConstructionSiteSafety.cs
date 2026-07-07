using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Construction;

internal sealed class ConstructionSiteSafety
{
    private readonly WorldModel _world;
    private readonly IConstructionDiffEmitter _diffEmitter;
    private readonly IConstructionJobLogger _logger;

    internal ConstructionSiteSafety(WorldModel world, IConstructionDiffEmitter diffEmitter, IConstructionJobLogger? logger)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
        _logger = logger ?? NullConstructionJobLogger.Instance;
    }

    internal bool IsAnchorOccupied(PlaceableInstance site)
    {
        return _world.Creatures.GetAllInstances()
            .Any(creature => creature.Z == site.Z && creature.Position.X == site.Position.X && creature.Position.Y == site.Position.Y);
    }

    internal bool TryRelocateAnchorOccupants(PlaceableInstance site)
    {
        var occupants = _world.Creatures.GetAllInstances()
            .Where(creature => creature.Z == site.Z && creature.Position.X == site.Position.X && creature.Position.Y == site.Position.Y)
            .ToList();
        if (occupants.Count == 0)
        {
            return false;
        }

        var safe = WorldSafetyQueries.FindNearestStandableNonConstructionSite(_world, site.Position.X, site.Position.Y, site.Z, 2);
        if (safe == null)
        {
            return false;
        }

        foreach (var creature in occupants)
        {
            _diffEmitter.MoveCreature(creature.Guid, new Point3(safe.Value.X, safe.Value.Y, safe.Value.Z));
        }

        return true;
    }

    internal void RelocateWallFootprintCreatures(PlaceableInstance site, ulong tick)
    {
        var creaturesOnFootprint = GetCreaturesOnFootprint(site);
        if (creaturesOnFootprint.Count == 0)
        {
            return;
        }

        _logger.Log($"[BUILD.EXEC] WALL: {creaturesOnFootprint.Count} creature(s) on footprint at ({site.Position.X},{site.Position.Y},{site.Z}), relocating before wall placement");
        foreach (var creature in creaturesOnFootprint)
        {
            var safe = FindSafeCellAround(site);
            if (safe.HasValue)
            {
                _diffEmitter.MoveCreature(creature.Guid, new Point3(safe.Value.X, safe.Value.Y, site.Z));
                _logger.Log($"[BUILD.EXEC] WALL: Relocated creature {creature.Guid} from ({creature.Position.X},{creature.Position.Y},{creature.Z}) to ({safe.Value.X},{safe.Value.Y},{site.Z})");
            }
            else
            {
                _logger.Log($"[BUILD.EXEC] WALL: WARNING - No safe cell found for creature {creature.Guid} at ({creature.Position.X},{creature.Position.Y},{creature.Z})");
            }
        }
    }

    internal void MoveResidualItemsOffAnchor(PlaceableInstance site)
    {
        var items = _world.Items.GetGroundItemsAt(site.Position, site.Z)
            .OrderBy(item => item.Guid)
            .ToList();
        if (items.Count == 0)
        {
            return;
        }

        var safe = FindSafeCellAround(site);
        if (!safe.HasValue)
        {
            return;
        }

        foreach (var item in items)
        {
            _diffEmitter.MoveItem(item.Guid, safe.Value, item.Z);
        }
    }

    private List<CreatureInstance> GetCreaturesOnFootprint(PlaceableInstance site)
    {
        var result = new List<CreatureInstance>();
        var footprint = site.Footprint;
        foreach (var creature in _world.Creatures.GetAllInstances())
        {
            if (creature.Z != site.Z)
            {
                continue;
            }

            int relX = creature.Position.X - site.Position.X;
            int relY = creature.Position.Y - site.Position.Y;
            if (relX >= 0 && relX < footprint.W && relY >= 0 && relY < footprint.D)
            {
                result.Add(creature);
            }
        }

        return result;
    }

    private Point? FindSafeCellAround(PlaceableInstance site)
    {
        var safe = WorldSafetyQueries.FindNearestStandableNonConstructionSite(_world, site.Position.X, site.Position.Y, site.Z, 2);
        return safe == null ? null : new Point(safe.Value.X, safe.Value.Y);
    }
}
