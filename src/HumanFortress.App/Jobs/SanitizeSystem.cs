using System;
using System.Linq;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;

namespace HumanFortress.App.Jobs;

/// <summary>
/// Low-frequency sanitizer that relocates any creatures/items stuck in non-walkable tiles
/// to a nearby safe cell. Acts as a safety net for rare races.
/// </summary>
public sealed class SanitizeSystem : ITick
{
    private readonly HumanFortress.Simulation.World.World _world;
    private int _counter;
    private readonly int _interval;
    private readonly int _maxPerTick;

    public SanitizeSystem(HumanFortress.Simulation.World.World world, int intervalTicks = 40, int maxPerTick = 8)
    {
        _world = world;
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
            var t = _world.GetTile(cr.Position.X, cr.Position.Y, cr.Z);
            bool bad = t == null || !(t.Value.IsStandable || t.Value.IsWalkable);
            if (!bad) continue;
            var safe = FindNearestStandableNonSite(cr.Position.X, cr.Position.Y, cr.Z, 3);
            if (safe != null)
            {
                cr.Position = new SadRogue.Primitives.Point(safe.Value.X, safe.Value.Y);
                cr.Z = safe.Value.Z;
                moved++;
                Logger.Log($"[SANITIZE] creature={cr.Guid} from=({cr.Position.X},{cr.Position.Y},{cr.Z}) to=({safe.Value.X},{safe.Value.Y},{safe.Value.Z})");
            }
        }
        // Items next
        foreach (var it in _world.Items.GetAllInstances().ToList())
        {
            if (moved >= _maxPerTick) break;
            if (it.IsCarried) continue;
            var t = _world.GetTile(it.Position.X, it.Position.Y, it.Z);
            bool bad = t == null || !(t.Value.IsStandable || t.Value.IsWalkable);
            if (!bad) continue;
            var safe = FindNearestStandableNonSite(it.Position.X, it.Position.Y, it.Z, 3);
            if (safe != null)
            {
                var old = it.Position;
                _world.Items.UpdateItemPosition(it.Guid, old, it.Z, new SadRogue.Primitives.Point(safe.Value.X, safe.Value.Y), safe.Value.Z);
                try { _world.Items.MergeStacksAt(new SadRogue.Primitives.Point(safe.Value.X, safe.Value.Y), safe.Value.Z); } catch {}
                moved++;
                Logger.Log($"[SANITIZE] item={it.Guid} from=({old.X},{old.Y},{it.Z}) to=({safe.Value.X},{safe.Value.Y},{safe.Value.Z})");
            }
        }
    }

    private struct P3 { public int X; public int Y; public int Z; public P3(int x,int y,int z){X=x;Y=y;Z=z;} }
    private P3? FindNearestStandableNonSite(int sx, int sy, int sz, int maxRadius)
    {
        var visited = new System.Collections.Generic.HashSet<(int,int)>();
        var q = new System.Collections.Generic.Queue<(int x,int y,int d)>();
        void Enq(int x,int y,int d){ if (!_world.IsValidPosition(x,y,sz)) return; if (visited.Add((x,y))) q.Enqueue((x,y,d)); }
        foreach (var (dx,dy) in new (int,int)[]{ (1,0),(-1,0),(0,1),(0,-1) }) Enq(sx+dx, sy+dy, 1);
        foreach (var (dx,dy) in new (int,int)[]{ (2,0),(-2,0),(0,2),(0,-2),(1,1),(1,-1),(-1,1),(-1,-1) }) Enq(sx+dx, sy+dy, 2);
        while (q.Count > 0)
        {
            var (x,y,d) = q.Dequeue();
            if (d > maxRadius) break;
            var tile = _world.GetTile(x,y,sz);
            if (tile == null) continue;
            if (!(tile.Value.IsStandable || tile.Value.IsWalkable)) continue;
            int cx = x / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int cy = y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int lx2 = x % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int ly2 = y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            var ck2 = new HumanFortress.Simulation.World.ChunkKey(cx, cy, sz);
            var ch2 = _world.GetChunk(ck2);
            bool bad = false;
            if (ch2 != null)
            {
                var pd = ch2.GetPlaceableData();
                if (pd != null && pd.TryGetOwnedAt(HumanFortress.Simulation.World.Chunk.LocalIndex(lx2,ly2), out var owned))
                {
                    if (owned.ConstructionSite != null) bad = true;
                }
            }
            if (bad) continue;
            return new P3(x,y,sz);
        }
        return null;
    }
}

