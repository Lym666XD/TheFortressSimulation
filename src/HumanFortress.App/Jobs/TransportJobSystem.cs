using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.App.Jobs;

/// <summary>
/// Transport executor (refactored HaulJobSystem):
/// - Drains transport requests from a decoupled queue (Simulation layer) in a deterministic order.
/// - Assigns workers, executes movement paths, and emits DiffOps for creature/item moves.
/// - Maintains backlog with carryover/TTL-friendly behavior and structured stats.
///
/// Concurrency model:
/// - ReadTick: queue drain + assignment (read-only world ops and reservations).
/// - WriteTick: movement updates + Diff emissions (Items/L5, Creatures/L6).
///
/// Determinism:
/// - Stable queue sorting keys; worker selection ordered by Creature GUID, ties use seeded RNG.
/// - Diff order uses UpdateOrder priority and stable encodings.
/// </summary>
public sealed class TransportJobSystem : ITick
{
    private readonly HumanFortress.Simulation.World.World _world;
    private readonly ITransportRequestQueue _requests;
    private readonly NavigationManager _nav;
    private readonly IPathService _paths;
    private readonly WorldNavigationView _navView;
    private readonly MovementExecutor _move;
    private readonly DiffLog? _diff;

    private readonly List<TransportRequest> _inboxBuffer = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<TransportRequest> _backlog = new();
    private readonly System.Collections.Generic.HashSet<System.Guid> _backlogIds = new();
    private readonly System.Collections.Generic.Dictionary<System.Guid, ulong> _backlogEnqueueTick = new();
    private readonly List<ActiveJob> _active = new();

    public int LastIntakeCount { get; private set; } = 0;
    private readonly int _maxIntakePerTick;
    private readonly int _carryoverMaxTicks;
    private const int CreatureReserveTtlTicks = 200;

    private int _lastCompletedTotal;
    private int _lastRequeuedTotal;
    private int _lastNoPathTotal;

    public readonly record struct JobStatsSnapshot(int Intake, int Active, int Backlog, int CompletedDelta, int RequeuedDelta, int NoPathDelta, int CarryoverOld);
    private JobStatsSnapshot _lastStats;
    public JobStatsSnapshot GetLastStatsSnapshot() => _lastStats;
    public int GetBacklogCount() => _backlog.Count;

    public TransportJobSystem(
        HumanFortress.Simulation.World.World world,
        ITransportRequestQueue requestQueue,
        DiffLog? diffLog = null,
        NavigationManager? sharedNav = null,
        int intakeBudget = 16,
        int carryoverMaxTicks = 8)
    {
        _world = world;
        _requests = requestQueue;
        _nav = sharedNav ?? new NavigationManager(world);
        _paths = new PathService(NavigationTuning.LoadFromContent());
        _navView = new WorldNavigationView(_nav, world);
        _move = new MovementExecutor(_paths);
        _diff = diffLog;
        _maxIntakePerTick = Math.Max(1, intakeBudget);
        _carryoverMaxTicks = Math.Max(1, carryoverMaxTicks);
    }

    public int Priority => UpdateOrder.Priority.Jobs;
    public string SystemId => "Jobs.Transport";

    public NavigationManager NavigationManager => _nav;

    public void ReadTick(ulong tick)
    {
        _paths.BeginTick();
        _inboxBuffer.Clear();

        // Drain backlog first
        while (_inboxBuffer.Count < _maxIntakePerTick && _backlog.TryDequeue(out var retry))
        {
            _inboxBuffer.Add(retry);
            _backlogIds.Remove(retry.ItemGuid);
            _backlogEnqueueTick.Remove(retry.ItemGuid);
        }

        // Drain fresh requests from queue deterministically
        if (_inboxBuffer.Count < _maxIntakePerTick)
        {
            _requests.Drain(_maxIntakePerTick - _inboxBuffer.Count, _inboxBuffer);
        }
        LastIntakeCount = _inboxBuffer.Count;

        if (_inboxBuffer.Count == 0)
        {
            if ((tick % 60UL) == 0UL)
                Logger.Log($"[TRANS-JOBS][{tick}] No requests dequeued.");
            _lastStats = new JobStatsSnapshot(0, _active.Count, _backlog.Count, 0, 0, 0, 0);
            return;
        }

        // De-dup by item, drop stale (already carried/reserved)
        var seen = new HashSet<Guid>();
        var reqs = _inboxBuffer
            .OrderBy(r => r.ItemGuid)
            .Where(r =>
            {
                if (!seen.Add(r.ItemGuid)) return false;
                var it = _world.Items.GetInstance(r.ItemGuid);
                if (it == null || it.IsCarried || it.IsReserved) return false;
                if (_world.Reservations.IsItemReserved(r.ItemGuid, tick)) return false;
                return true;
            })
            .ToList();

        var creatures = _world.Creatures.GetAllInstances().OrderBy(c => c.Guid).ToList();
        Logger.Log($"[TRANS-JOBS][{tick}] Intake={reqs.Count} Active={_active.Count} Backlog={_backlog.Count} Workers={creatures.Count}");

        var busy = new HashSet<Guid>(_active.Select(a => a.CreatureId));
        foreach (var rq in reqs)
        {
            bool assigned = false;
            foreach (var worker in creatures)
            {
                if (worker.HP <= 0) continue;
                if (busy.Contains(worker.Guid)) continue;
                if (!_world.Reservations.TryReserveCreature(worker.Guid, SystemId, tick + (ulong)CreatureReserveTtlTicks, jobId: $"haul:{rq.ItemGuid}"))
                    continue;

                // Reserve the item to avoid assigning multiple workers to the same source stack
                if (!_world.Reservations.TryReserveItem(rq.ItemGuid, worker.Guid, tick + (ulong)CreatureReserveTtlTicks))
                {
                    _world.Reservations.ReleaseCreature(worker.Guid);
                    continue;
                }

                var start = new HumanFortress.Navigation.Point3(worker.Position.X, worker.Position.Y, worker.Z);
                var toItem = new HumanFortress.Navigation.Point3(rq.From.X, rq.From.Y, rq.FromZ);
                var req = new PathRequest(start, toItem, MoveMode.Walk, PathFlags.AllowDiagonal, SeedFrom(worker.Guid, rq.ItemGuid));
                IWorldNavigationView view = _navView;
                var path = _paths.Solve(in req, in view);
                if (path.Kind != PathResultKind.Found)
                {
                    _world.Reservations.ReleaseCreature(worker.Guid);
                    _world.Reservations.ReleaseItem(rq.ItemGuid);
                    continue;
                }

                _move.BeginMovement(ToEntity(worker.Guid), req, path);
                int qty = rq.Quantity > 0 ? rq.Quantity : (_world.Items.GetInstance(rq.ItemGuid)?.StackCount ?? 1);
                _active.Add(new ActiveJob
                {
                    CreatureId = worker.Guid,
                    ItemId = rq.ItemGuid,
                    Dest = new HumanFortress.Navigation.Point3(rq.To.X, rq.To.Y, rq.ToZ),
                    Stage = JobStage.ToItem,
                    Quantity = qty
                });
                busy.Add(worker.Guid);
                assigned = true;
                JobStats.Assigned++;
                Logger.Log($"[TRANS-JOBS][{tick}] Assigned worker={worker.Guid} item={rq.ItemGuid} -> ToItem ({rq.From.X},{rq.From.Y},{rq.FromZ})");
                break;
            }
            if (!assigned)
            {
                if (_backlogIds.Add(rq.ItemGuid))
                {
                    _backlog.Enqueue(rq);
                    _backlogEnqueueTick[rq.ItemGuid] = tick;
                }
            }
        }

        // Stats snapshot (carryover age)
        int carryoverOld = 0;
        foreach (var kv in _backlogEnqueueTick)
        {
            var age = (int)(tick - kv.Value);
            if (age > (_carryoverMaxTicks * 2)) carryoverOld++;
        }
        _lastStats = new JobStatsSnapshot(LastIntakeCount, _active.Count, _backlog.Count,
            CompletedDelta: JobStats.Completed - _lastCompletedTotal,
            RequeuedDelta: JobStats.Requeued - _lastRequeuedTotal,
            NoPathDelta: JobStats.NoPath - _lastNoPathTotal,
            CarryoverOld: carryoverOld);
    }

    public void WriteTick(ulong tick)
    {
        if (_active.Count == 0) return;
        var finished = new List<ActiveJob>();
        foreach (var job in _active)
        {
            var cr = _world.Creatures.GetInstance(job.CreatureId);
            if (cr == null) { finished.Add(job); _world.Reservations.ReleaseCreature(job.CreatureId); continue; }
            uint eid = ToEntity(cr.Guid);

            // refresh creature reservation TTL while job is active
            _world.Reservations.TryReserveCreature(job.CreatureId, SystemId, tick + (ulong)CreatureReserveTtlTicks, jobId: $"haul:{job.ItemId}");

            var update = _move.UpdateMovement(eid, _navView);
            if (update.NeedsReplan)
            {
                var src = update.Position;
                HumanFortress.Navigation.Point3 goal = job.Stage == JobStage.ToItem ? GetItemPos(job) : job.Dest;
                var req = new PathRequest(src, goal, MoveMode.Walk, PathFlags.AllowDiagonal, SeedFrom(job.CreatureId, job.ItemId));
                IWorldNavigationView view2 = _navView;
                var path = _paths.Solve(in req, in view2);
                if (path.Kind == PathResultKind.Found)
                {
                    _move.BeginMovement(eid, req, path);
                }
                else
                {
                    // Unstuck logic for repeated invalid replans from a non-walkable cell
                    if (path.Kind == PathResultKind.Invalid)
                    {
                        job.InvalidReplanCount++;
                        var t = _world.GetTile(src.X, src.Y, src.Z);
                        bool srcBad = t == null || !(t.Value.IsStandable || t.Value.IsWalkable);
                        if (job.InvalidReplanCount >= 2 && srcBad)
                        {
                            var safe = FindNearestStandableNonSite(src.X, src.Y, src.Z, 3);
                            if (safe != null)
                            {
                                EmitMoveCreatureDiff(eid, safe.Value);
                                job.InvalidReplanCount = 0;
                                var req2 = new PathRequest(safe.Value, goal, MoveMode.Walk, PathFlags.AllowDiagonal, SeedFrom(job.CreatureId, job.ItemId));
                                var path2 = _paths.Solve(in req2, in view2);
                                if (path2.Kind == PathResultKind.Found)
                                {
                                    _move.BeginMovement(eid, req2, path2);
                                }
                                Logger.Log($"[TRANS-JOBS][{tick}] UNSTUCK worker={job.CreatureId} from=({src.X},{src.Y},{src.Z}) to=({safe.Value.X},{safe.Value.Y},{safe.Value.Z}) kind={path2.Kind}");
                            }
                        }
                    }
                }
                Logger.Log($"[TRANS-JOBS][{tick}] Replan worker={job.CreatureId} stage={job.Stage} from=({src.X},{src.Y},{src.Z}) goal=({goal.X},{goal.Y},{goal.Z}) kind={path.Kind}");
                continue;
            }

            EmitMoveCreatureDiff(eid, update.Position);

            if (update.Status == MovementStatus.Arrived || update.Status == MovementStatus.PathComplete)
            {
                if (job.Stage == JobStage.ToItem)
                {
                    // If requested quantity is less than the stack, split a new stack to carry
                    var inst = _world.Items.GetInstance(job.ItemId);
                    if (inst != null && job.Quantity > 0 && inst.StackCount > job.Quantity)
                    {
                        var newId = _world.Items.SplitStack(job.ItemId, job.Quantity);
                        if (newId.HasValue)
                        {
                            Logger.Log($"[TRANS-JOBS][{tick}] Split stack old={job.ItemId} new={newId} take={job.Quantity}");
                            job.ItemId = newId.Value;
                        }
                    }
                    // Mark carried on pickup
                    EmitMarkCarried(job.ItemId, job.CreatureId, update.Position);
                    var req2 = new PathRequest(update.Position, job.Dest, MoveMode.Walk, PathFlags.AllowDiagonal, SeedFrom(job.CreatureId, job.ItemId));
                    IWorldNavigationView view3 = _navView;
                    var path2 = _paths.Solve(in req2, in view3);
                    if (path2.Kind == PathResultKind.Found)
                    {
                        _move.BeginMovement(eid, req2, path2);
                        job.Stage = JobStage.ToDest;
                        Logger.Log($"[TRANS-JOBS][{tick}] Picked item={job.ItemId}; now moving to dest=({job.Dest.X},{job.Dest.Y},{job.Dest.Z})");
                    }
                    else
                    {
                        finished.Add(job);
                        JobStats.NoPath++;
                    }
                }
                else if (job.Stage == JobStage.ToDest)
                {
                    // Place item and clear carried state
                    EmitMoveItem(job.ItemId, job.Dest);
                    EmitUnmarkCarried(job.ItemId, job.Dest);
                    _world.Reservations.ReleaseItem(job.ItemId);
                    _world.Reservations.ReleaseCreature(job.CreatureId);
                    finished.Add(job);
                    JobStats.Completed++;
                    Logger.Log($"[TRANS-JOBS][{tick}] Completed item={job.ItemId} to=({job.Dest.X},{job.Dest.Y},{job.Dest.Z}) by worker={job.CreatureId}");
                }
            }
        }
        if (finished.Count > 0)
        {
            foreach (var f in finished) _active.Remove(f);
            _lastCompletedTotal = JobStats.Completed;
            _lastNoPathTotal = JobStats.NoPath;
            _lastRequeuedTotal = JobStats.Requeued;
        }
    }

    public readonly record struct ActiveJobView(Guid CreatureId, Guid ItemId, HumanFortress.Navigation.Point3 FromOrCurrent, HumanFortress.Navigation.Point3 Dest, string Stage);

    public List<ActiveJobView> GetActiveJobsSnapshot()
    {
        var list = new List<ActiveJobView>(_active.Count);
        foreach (var j in _active)
        {
            var from = j.Stage == JobStage.ToItem ? GetItemPos(j) : GetCreaturePos(j.CreatureId);
            list.Add(new ActiveJobView(j.CreatureId, j.ItemId, from, j.Dest, j.Stage.ToString()));
        }
        return list;
    }

    private enum JobStage { ToItem, ToDest }

    private sealed class ActiveJob
    {
        public Guid CreatureId { get; set; }
        public Guid ItemId { get; set; }
        public HumanFortress.Navigation.Point3 Dest { get; set; }
        public JobStage Stage { get; set; }
        public int Quantity { get; set; }
        public int InvalidReplanCount { get; set; }
    }

    private static uint ToEntity(Guid g)
    {
        var bytes = g.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
    }

    private HumanFortress.Navigation.Point3 GetCreaturePos(Guid creatureId)
    {
        var cr = _world.Creatures.GetInstance(creatureId);
        if (cr == null) return new HumanFortress.Navigation.Point3(0, 0, 0);
        return new HumanFortress.Navigation.Point3(cr.Position.X, cr.Position.Y, cr.Z);
    }

    private HumanFortress.Navigation.Point3 GetItemPos(ActiveJob job)
    {
        var it = _world.Items.GetInstance(job.ItemId);
        if (it == null) return new HumanFortress.Navigation.Point3(0, 0, 0);
        return new HumanFortress.Navigation.Point3(it.Position.X, it.Position.Y, it.Z);
    }

    private static uint SeedFrom(Guid a, Guid b)
    {
        unchecked
        {
            var ba = a.ToByteArray();
            var bb = b.ToByteArray();
            uint s = 2166136261;
            foreach (var t in ba) s = (s ^ t) * 16777619;
            foreach (var t in bb) s = (s ^ t) * 16777619;
            return s;
        }
    }

    private void EmitMoveCreatureDiff(uint entityId, HumanFortress.Navigation.Point3 position)
    {
        if (_diff == null) return;
        int chunkX = position.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int chunkY = position.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localX = position.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localY = position.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localIndex = localY * HumanFortress.Simulation.World.Chunk.SIZE_XY + localX;
        int chunkId = EncodeChunkId(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, position.Z));
        var target = new DiffTarget(chunkId, localIndex, unchecked((int)entityId));
        _diff.AddOp(new DiffOp(DiffOpType.MoveCreature, target, SystemId, Priority));
    }

    private void EmitMoveItem(Guid itemId, HumanFortress.Navigation.Point3 dest)
    {
        if (_diff == null) return;
        uint eid = ToEntity(itemId);
        int chunkX = dest.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int chunkY = dest.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localX = dest.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localY = dest.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localIndex = localY * HumanFortress.Simulation.World.Chunk.SIZE_XY + localX;
        int chunkId = EncodeChunkId(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, dest.Z));
        var target = new DiffTarget(chunkId, localIndex, unchecked((int)eid));
        _diff.AddOp(new DiffOp(DiffOpType.MoveItem, target, SystemId, Priority));
    }

    private void EmitMarkCarried(Guid itemId, Guid carrierId, HumanFortress.Navigation.Point3 at)
    {
        if (_diff == null) return;
        uint eidItem = ToEntity(itemId);
        uint eidCarrier = ToEntity(carrierId);
        int chunkX = at.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int chunkY = at.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localX = at.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localY = at.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localIndex = localY * HumanFortress.Simulation.World.Chunk.SIZE_XY + localX;
        int chunkId = EncodeChunkId(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, at.Z));
        var target = new DiffTarget(chunkId, localIndex, unchecked((int)eidItem));
        ulong args = eidCarrier; // low 32 bits carry carrier entity id
        _diff.AddOp(new DiffOp(DiffOpType.MarkCarried, target, SystemId, Priority, args));
    }

    private void EmitUnmarkCarried(Guid itemId, HumanFortress.Navigation.Point3 at)
    {
        if (_diff == null) return;
        uint eidItem = ToEntity(itemId);
        int chunkX = at.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int chunkY = at.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localX = at.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localY = at.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localIndex = localY * HumanFortress.Simulation.World.Chunk.SIZE_XY + localX;
        int chunkId = EncodeChunkId(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, at.Z));
        var target = new DiffTarget(chunkId, localIndex, unchecked((int)eidItem));
        _diff.AddOp(new DiffOp(DiffOpType.UnmarkCarried, target, SystemId, Priority));
    }

    private HumanFortress.Navigation.Point3? FindNearestStandableNonSite(int startX, int startY, int z, int maxRadius)
    {
        var visited = new System.Collections.Generic.HashSet<(int,int)>();
        var q = new System.Collections.Generic.Queue<(int x,int y,int d)>();
        void Enq(int x,int y,int d){ if (!_world.IsValidPosition(x,y,z)) return; if (visited.Add((x,y))) q.Enqueue((x,y,d)); }
        foreach (var (dx,dy) in new (int,int)[]{ (1,0),(-1,0),(0,1),(0,-1) }) Enq(startX+dx, startY+dy, 1);
        foreach (var (dx,dy) in new (int,int)[]{ (2,0),(-2,0),(0,2),(0,-2),(1,1),(1,-1),(-1,1),(-1,-1) }) Enq(startX+dx, startY+dy, 2);

        while (q.Count > 0)
        {
            var (x,y,d) = q.Dequeue();
            if (d > maxRadius) break;
            var tile = _world.GetTile(x,y,z);
            if (tile == null) continue;
            if (!(tile.Value.IsStandable || tile.Value.IsWalkable)) continue;
            int cx = x / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int cy = y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int lx = x % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int ly = y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            var ck = new HumanFortress.Simulation.World.ChunkKey(cx, cy, z);
            var chunk = _world.GetChunk(ck);
            bool bad = false;
            if (chunk != null)
            {
                var pd = chunk.GetPlaceableData();
                if (pd != null && pd.TryGetOwnedAt(HumanFortress.Simulation.World.Chunk.LocalIndex(lx,ly), out var owned))
                {
                    if (owned.ConstructionSite != null) bad = true;
                }
            }
            if (bad) continue;
            return new HumanFortress.Navigation.Point3(x,y,z);
        }
        return null;
    }

    private static int EncodeChunkId(HumanFortress.Simulation.World.ChunkKey ck)
    {
        int x = ck.ChunkX & 0x3FF;
        int y = ck.ChunkY & 0x3FF;
        int z = ck.Z & 0x3FF;
        return (z << 20) | (x << 10) | y;
    }
}

public static class JobStats
{
    public static int Assigned;
    public static int Completed;
    public static int NoPath;
    public static int Requeued;
}
