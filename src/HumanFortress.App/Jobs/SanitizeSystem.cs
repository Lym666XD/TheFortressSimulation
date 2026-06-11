using System;
using System.Linq;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Jobs;

/// <summary>
/// Low-frequency sanitizer that relocates any creatures/items stuck in non-walkable tiles
/// to a nearby safe cell. Acts as a safety net for rare races.
/// </summary>
public sealed class SanitizeSystem : ITick
{
    private readonly HumanFortress.Simulation.World.World _world;
    private readonly DiffLog? _diff;
    private int _counter;
    private readonly int _interval;
    private readonly int _maxPerTick;

    public SanitizeSystem(HumanFortress.Simulation.World.World world, DiffLog? diffLog = null, int intervalTicks = 40, int maxPerTick = 8)
    {
        _world = world;
        _diff = diffLog;
        _interval = Math.Max(5, intervalTicks);
        _maxPerTick = Math.Max(1, maxPerTick);
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
        // Creatures first
        foreach (var cr in _world.Creatures.GetAllInstances().ToList())
        {
            if (moved >= _maxPerTick) break;
            bool bad = !WorldSafetyQueries.IsStandableOrWalkable(_world, cr.Position.X, cr.Position.Y, cr.Z);
            if (!bad) continue;
            var safe = WorldSafetyQueries.FindNearestStandableNonConstructionSite(_world, cr.Position.X, cr.Position.Y, cr.Z, 3);
            if (safe != null)
            {
                var old = cr.Position;
                int oldZ = cr.Z;
                if (EmitMoveCreature(cr.Guid, safe.Value))
                {
                    moved++;
                    Logger.Log($"[SANITIZE] creature={cr.Guid} from=({old.X},{old.Y},{oldZ}) to=({safe.Value.X},{safe.Value.Y},{safe.Value.Z})");
                }
            }
        }
        // Items next
        foreach (var it in _world.Items.GetGroundInstances().ToList())
        {
            if (moved >= _maxPerTick) break;
            bool bad = !WorldSafetyQueries.IsStandableOrWalkable(_world, it.Position.X, it.Position.Y, it.Z);
            if (!bad) continue;
            var safe = WorldSafetyQueries.FindNearestStandableNonConstructionSite(_world, it.Position.X, it.Position.Y, it.Z, 3);
            if (safe != null)
            {
                var old = it.Position;
                int oldZ = it.Z;
                if (EmitMoveItem(it.Guid, safe.Value))
                {
                    moved++;
                    Logger.Log($"[SANITIZE] item={it.Guid} from=({old.X},{old.Y},{oldZ}) to=({safe.Value.X},{safe.Value.Y},{safe.Value.Z})");
                }
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
