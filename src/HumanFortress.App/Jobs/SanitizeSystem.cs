using System;
using System.Linq;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using WorldChunk = HumanFortress.Simulation.World.Chunk;
using WorldChunkKey = HumanFortress.Simulation.World.ChunkKey;

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
            var t = _world.GetTile(cr.Position.X, cr.Position.Y, cr.Z);
            bool bad = t == null || !(t.Value.IsStandable || t.Value.IsWalkable);
            if (!bad) continue;
            var safe = FindNearestStandableNonSite(cr.Position.X, cr.Position.Y, cr.Z, 3);
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
                int oldZ = it.Z;
                if (EmitMoveItem(it.Guid, safe.Value))
                {
                    moved++;
                    Logger.Log($"[SANITIZE] item={it.Guid} from=({old.X},{old.Y},{oldZ}) to=({safe.Value.X},{safe.Value.Y},{safe.Value.Z})");
                }
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

    private bool EmitMoveCreature(Guid creatureId, P3 dest)
    {
        if (_diff == null || creatureId == Guid.Empty) return false;
        if (!TryEncodeTarget(dest, out var chunkKey, out int localIndex)) return false;

        uint eid = ToEntity(creatureId);
        int chunkId = EncodeChunkId(chunkKey);
        var target = new DiffTarget(chunkId, localIndex, unchecked((int)eid));
        _diff.AddOp(new DiffOp(DiffOpType.MoveCreature, target, SystemId, Priority));
        return true;
    }

    private bool EmitMoveItem(Guid itemId, P3 dest)
    {
        if (_diff == null || itemId == Guid.Empty) return false;
        if (!TryEncodeTarget(dest, out var chunkKey, out int localIndex)) return false;

        uint eid = ToEntity(itemId);
        int chunkId = EncodeChunkId(chunkKey);
        var target = new DiffTarget(chunkId, localIndex, unchecked((int)eid));
        _diff.AddOp(new DiffOp(DiffOpType.MoveItem, target, SystemId, Priority));
        return true;
    }

    private static bool TryEncodeTarget(P3 dest, out WorldChunkKey chunkKey, out int localIndex)
    {
        chunkKey = default;
        localIndex = 0;
        if (dest.X < 0 || dest.Y < 0 || dest.Z < 0) return false;

        int cx = dest.X / WorldChunk.SIZE_XY;
        int cy = dest.Y / WorldChunk.SIZE_XY;
        int lx = dest.X % WorldChunk.SIZE_XY;
        int ly = dest.Y % WorldChunk.SIZE_XY;
        chunkKey = new WorldChunkKey(cx, cy, dest.Z);
        localIndex = WorldChunk.LocalIndex(lx, ly);
        return true;
    }

    private static int EncodeChunkId(WorldChunkKey ck)
    {
        return ((ck.Z & 0x3FF) << 20) | ((ck.ChunkX & 0x3FF) << 10) | (ck.ChunkY & 0x3FF);
    }

    private static uint ToEntity(Guid g)
    {
        var bytes = g.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
    }
}
