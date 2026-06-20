using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.World;

namespace HumanFortress.Jobs;

/// <summary>
/// Low-frequency sanitizer that relocates any creatures/items stuck in non-walkable tiles
/// to a nearby safe cell.
/// </summary>
public sealed class SanitizeSystem : ITick
{
    private readonly HumanFortress.Simulation.World.World _world;
    private readonly DiffLog? _diff;
    private readonly Action<string>? _log;
    private int _counter;
    private readonly int _interval;
    private readonly int _maxPerTick;

    public SanitizeSystem(
        HumanFortress.Simulation.World.World world,
        DiffLog? diffLog = null,
        int intervalTicks = 40,
        int maxPerTick = 8,
        Action<string>? log = null)
    {
        _world = world;
        _diff = diffLog;
        _interval = Math.Max(5, intervalTicks);
        _maxPerTick = Math.Max(1, maxPerTick);
        _log = log;
    }

    public int Priority => UpdateOrder.Priority.Jobs;
    public string SystemId => "Jobs.Sanitize";

    public void ReadTick(ulong tick)
    {
        // no-op
    }

    public void WriteTick(ulong tick)
    {
        _counter++;
        if ((_counter % _interval) != 0) return;

        int moved = 0;
        foreach (var creature in _world.Creatures.GetAllInstances().ToList())
        {
            if (moved >= _maxPerTick) break;
            bool bad = !WorldSafetyQueries.IsStandableOrWalkable(_world, creature.Position.X, creature.Position.Y, creature.Z);
            if (!bad) continue;

            var safe = WorldSafetyQueries.FindNearestStandableNonConstructionSite(_world, creature.Position.X, creature.Position.Y, creature.Z, 3);
            if (safe == null) continue;

            var old = creature.Position;
            int oldZ = creature.Z;
            if (EmitMoveCreature(creature.Guid, safe.Value))
            {
                moved++;
                _log?.Invoke($"[SANITIZE] creature={creature.Guid} from=({old.X},{old.Y},{oldZ}) to=({safe.Value.X},{safe.Value.Y},{safe.Value.Z})");
            }
        }

        foreach (var item in _world.Items.GetGroundInstances().ToList())
        {
            if (moved >= _maxPerTick) break;
            bool bad = !WorldSafetyQueries.IsStandableOrWalkable(_world, item.Position.X, item.Position.Y, item.Z);
            if (!bad) continue;

            var safe = WorldSafetyQueries.FindNearestStandableNonConstructionSite(_world, item.Position.X, item.Position.Y, item.Z, 3);
            if (safe == null) continue;

            var old = item.Position;
            int oldZ = item.Z;
            if (EmitMoveItem(item.Guid, safe.Value))
            {
                moved++;
                _log?.Invoke($"[SANITIZE] item={item.Guid} from=({old.X},{old.Y},{oldZ}) to=({safe.Value.X},{safe.Value.Y},{safe.Value.Z})");
            }
        }
    }

    private bool EmitMoveCreature(Guid creatureId, WorldPoint3 dest)
    {
        if (_diff == null || creatureId == Guid.Empty) return false;
        if (!WorldCellTargetEncoding.TryEncode(dest.X, dest.Y, dest.Z, out var target)) return false;

        _diff.AddOp(new DiffOp(DiffOpType.MoveCreature, target.ToDiffTarget(DiffTargetEncoding.SignedEntityId(creatureId)), SystemId, Priority));
        return true;
    }

    private bool EmitMoveItem(Guid itemId, WorldPoint3 dest)
    {
        if (_diff == null || itemId == Guid.Empty) return false;
        if (!WorldCellTargetEncoding.TryEncode(dest.X, dest.Y, dest.Z, out var target)) return false;

        _diff.AddOp(new DiffOp(DiffOpType.MoveItem, target.ToDiffTarget(DiffTargetEncoding.SignedEntityId(itemId)), SystemId, Priority));
        return true;
    }
}
