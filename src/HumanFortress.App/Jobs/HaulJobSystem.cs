using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Orders;

namespace HumanFortress.App.Jobs;

/// <summary>
/// Executes haul jobs by assigning creatures and moving them along paths.
/// v1 skeleton used in App layer to avoid project cycles.
/// </summary>
public sealed class HaulJobSystem : ITick
{
    private readonly HumanFortress.Simulation.World.World _world;
    private readonly HaulingSystem _planner;
    private readonly NavigationManager _nav;
    private readonly IPathService _paths;
    private readonly WorldNavigationView _navView;
    private readonly MovementExecutor _move;
    private readonly DiffLog? _diff;

    private readonly List<ActiveJob> _active = new();
    private readonly List<HaulingSystem.PlannedMove> _inboxBuffer = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<HaulingSystem.PlannedMove> _backlog = new();

    public HaulJobSystem(HumanFortress.Simulation.World.World world, HaulingSystem planner, DiffLog? diffLog = null)
    {
        _world = world;
        _planner = planner;
        _nav = new NavigationManager(world);
        _paths = new PathService();
        _navView = new WorldNavigationView(_nav, world);
        _move = new MovementExecutor(_paths);
        _diff = diffLog;
        _nav.RebuildAll();
    }

    public int Priority => UpdateOrder.Priority.Jobs;
    public string SystemId => "Jobs.HaulJobSystem";

    public void ReadTick(ulong tick)
    {
        _paths.BeginTick();
        _inboxBuffer.Clear();
        // First, drain backlog (retry moves)
        while (_inboxBuffer.Count < 16 && _backlog.TryDequeue(out var retry))
            _inboxBuffer.Add(retry);
        // Then, new planned moves from planner
        if (_inboxBuffer.Count < 16)
            _planner.DequeuePlannedMoves(16 - _inboxBuffer.Count, _inboxBuffer);
        if (_inboxBuffer.Count == 0)
        {
            Logger.Log($"[HAULJOBS][{tick}] No planned moves dequeued.");
            return;
        }

        // De-duplicate by item and drop stale ones (already reserved/carried)
        var seen = new HashSet<Guid>();
        var moves = _inboxBuffer
            .OrderBy(m => m.ItemGuid)
            .Where(m =>
            {
                if (!seen.Add(m.ItemGuid)) return false;
                var it = _world.Items.GetInstance(m.ItemGuid);
                return it != null && !it.IsCarried && !it.IsReserved;
            })
            .ToList();
        var creatures = _world.Creatures.GetAllInstances().OrderBy(c => c.Guid).ToList();
        Logger.Log($"[HAULJOBS][{tick}] Moves={moves.Count} Creatures={creatures.Count}");
        IWorldNavigationView view = _navView;
        var busy = new HashSet<Guid>(_active.Select(a => a.CreatureId));
        foreach (var mv in moves)
        {
            bool assigned = false;
            foreach (var worker in creatures)
            {
                if (worker.HP <= 0) continue; // allow cross-Z by pathfinding
                if (busy.Contains(worker.Guid)) continue; // already on a job
                var req = new PathRequest(new Point3(worker.Position.X, worker.Position.Y, worker.Z), new Point3(mv.From.X, mv.From.Y, mv.FromZ), MoveMode.Walk, PathFlags.AllowDiagonal, SeedFrom(worker.Guid, mv.ItemGuid));
                var path = _paths.Solve(in req, in view);
                if (path.Kind != PathResultKind.Found)
                {
                    Logger.Log($"[HAULJOBS][{tick}] No path to item for worker={worker.Guid} from=({worker.Position.X},{worker.Position.Y},{worker.Z}) to=({mv.From.X},{mv.From.Y},{mv.FromZ})");
                    JobStats.NoPath++;
                    continue;
                }
                var job = new ActiveJob
                {
                    CreatureId = worker.Guid,
                    ItemId = mv.ItemGuid,
                    Dest = new Point3(mv.To.X, mv.To.Y, mv.ToZ),
                    Stage = JobStage.ToItem
                };
                _move.BeginMovement(ToEntity(worker.Guid), req, path);
                _active.Add(job);
                busy.Add(worker.Guid);
                // Reserve the item to prevent duplicate planning/assignment
                var it = _world.Items.GetInstance(mv.ItemGuid);
                if (it != null)
                {
                    it.IsReserved = true;
                    it.ReservedBy = worker.Guid;
                }
                Logger.Log($"[HAULJOBS][{tick}] Assigned worker={worker.Guid} -> item={mv.ItemGuid} dest=({mv.To.X},{mv.To.Y},{mv.ToZ})");
                assigned = true;
                JobStats.Assigned++;
                break;
            }
            if (!assigned)
            {
                // Requeue for retry next tick
                _backlog.Enqueue(mv);
                Logger.Log($"[HAULJOBS][{tick}] No available worker for item={mv.ItemGuid}; requeue.");
                JobStats.Requeued++;
            }
        }
    }

    public void WriteTick(ulong tick)
    {
        if (_active.Count == 0) return;
        var finished = new List<ActiveJob>();
        foreach (var job in _active)
        {
            var cr = _world.Creatures.GetInstance(job.CreatureId);
            if (cr == null) { finished.Add(job); continue; }
            uint eid = ToEntity(cr.Guid);
            var update = _move.UpdateMovement(eid, _navView);
            if (update.NeedsReplan)
            {
                var src = update.Position;
                Point3 goal = job.Stage == JobStage.ToItem ? GetItemPos(job) : job.Dest;
                IWorldNavigationView view2 = _navView;
                var req = new PathRequest(src, goal, MoveMode.Walk, PathFlags.None, SeedFrom(job.CreatureId, job.ItemId));
                var path = _paths.Solve(in req, in view2);
                if (path.Kind == PathResultKind.Found)
                    _move.BeginMovement(eid, req, path);
                Logger.Log($"[HAULJOBS][{tick}] Replan worker={job.CreatureId} stage={job.Stage} from=({src.X},{src.Y},{src.Z}) goal=({goal.X},{goal.Y},{goal.Z}) kind={path.Kind}");
                continue;
            }

            // Emit Diff for creature movement (runtime updated by applicator)
            EmitMoveCreatureDiff(eid, update.Position);

            if (update.Status == MovementStatus.Arrived || update.Status == MovementStatus.PathComplete)
            {
                if (job.Stage == JobStage.ToItem)
                {
                    IWorldNavigationView view3 = _navView;
                    // Mark carried when item picked up (via Diff)
                    EmitMarkCarried(job.ItemId, job.CreatureId, update.Position);
                    var req2 = new PathRequest(update.Position, job.Dest, MoveMode.Walk, PathFlags.AllowDiagonal, SeedFrom(job.CreatureId, job.ItemId));
                    var path2 = _paths.Solve(in req2, in view3);
                    if (path2.Kind == PathResultKind.Found)
                    {
                        _move.BeginMovement(eid, req2, path2);
                        job.Stage = JobStage.ToDest;
                        Logger.Log($"[HAULJOBS][{tick}] Picked item={job.ItemId}; now moving to dest=({job.Dest.X},{job.Dest.Y},{job.Dest.Z})");
                    }
                    else finished.Add(job);
                }
                else if (job.Stage == JobStage.ToDest)
                {
                    // Place item via Diff and clear carried state
                    EmitMoveItem(job.ItemId, job.Dest);
                    EmitUnmarkCarried(job.ItemId, job.Dest);
                    finished.Add(job);
                    Logger.Log($"[HAULJOBS][{tick}] Completed haul item={job.ItemId} to=({job.Dest.X},{job.Dest.Y},{job.Dest.Z}) by worker={job.CreatureId}");
                    JobStats.Completed++;
                }
            }
        }
        if (finished.Count > 0)
            foreach (var f in finished) _active.Remove(f);
    }

    private static uint ToEntity(Guid g)
    {
        var bytes = g.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
    }

    private Point3 GetItemPos(ActiveJob job)
    {
        var it = _world.Items.GetInstance(job.ItemId);
        if (it == null) return new Point3(0, 0, 0);
        return new Point3(it.Position.X, it.Position.Y, it.Z);
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

    private enum JobStage { ToItem, ToDest }

    private class ActiveJob
    {
        public Guid CreatureId { get; set; }
        public Guid ItemId { get; set; }
        public Point3 Dest { get; set; }
        public JobStage Stage { get; set; }
    }

    private void EmitMoveCreatureDiff(uint entityId, Point3 position)
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

    private void EmitMoveItem(Guid itemId, Point3 dest)
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

    private void EmitMarkCarried(Guid itemId, Guid carrierId, Point3 at)
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

    private void EmitUnmarkCarried(Guid itemId, Point3 at)
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

    private static int EncodeChunkId(HumanFortress.Simulation.World.ChunkKey ck)
    {
        // 10 bits each for x,y,z -> supports up to 1024 in each dimension
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

