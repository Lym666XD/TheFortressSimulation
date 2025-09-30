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

    private readonly List<ActiveJob> _active = new();
    private readonly List<HaulingSystem.PlannedMove> _inboxBuffer = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<HaulingSystem.PlannedMove> _backlog = new();

    public HaulJobSystem(HumanFortress.Simulation.World.World world, HaulingSystem planner)
    {
        _world = world;
        _planner = planner;
        _nav = new NavigationManager(world);
        _paths = new PathService();
        _navView = new WorldNavigationView(_nav, world);
        _move = new MovementExecutor(_paths);
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

        var moves = _inboxBuffer.OrderBy(m => m.ItemGuid).ToList();
        var creatures = _world.Creatures.GetAllInstances().OrderBy(c => c.Guid).ToList();
        Logger.Log($"[HAULJOBS][{tick}] Moves={moves.Count} Creatures={creatures.Count}");
        IWorldNavigationView view = _navView;
        var busy = new HashSet<Guid>(_active.Select(a => a.CreatureId));
        foreach (var mv in moves)
        {
            bool assigned = false;
            foreach (var worker in creatures)
            {
                if (worker.Z != mv.FromZ || worker.HP <= 0) continue;
                if (busy.Contains(worker.Guid)) continue; // already on a job
                var req = new PathRequest(new Point3(worker.Position.X, worker.Position.Y, worker.Z), new Point3(mv.From.X, mv.From.Y, mv.FromZ), MoveMode.Walk, PathFlags.None, SeedFrom(worker.Guid, mv.ItemGuid));
                var path = _paths.Solve(in req, in view);
                if (path.Kind != PathResultKind.Found)
                {
                    Logger.Log($"[HAULJOBS][{tick}] No path to item for worker={worker.Guid} from=({worker.Position.X},{worker.Position.Y},{worker.Z}) to=({mv.From.X},{mv.From.Y},{mv.FromZ})");
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
                Logger.Log($"[HAULJOBS][{tick}] Assigned worker={worker.Guid} -> item={mv.ItemGuid} dest=({mv.To.X},{mv.To.Y},{mv.ToZ})");
                assigned = true;
                break;
            }
            if (!assigned)
            {
                // Requeue for retry next tick
                _backlog.Enqueue(mv);
                Logger.Log($"[HAULJOBS][{tick}] No available worker for item={mv.ItemGuid}; requeue.");
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

            cr.Position = new SadRogue.Primitives.Point(update.Position.X, update.Position.Y);
            cr.Z = update.Position.Z;

            if (update.Status == MovementStatus.Arrived || update.Status == MovementStatus.PathComplete)
            {
                if (job.Stage == JobStage.ToItem)
                {
                    IWorldNavigationView view3 = _navView;
                    var req2 = new PathRequest(update.Position, job.Dest, MoveMode.Walk, PathFlags.None, SeedFrom(job.CreatureId, job.ItemId));
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
                    var item = _world.Items.GetInstance(job.ItemId);
                    if (item != null)
                    {
                        item.Position = new SadRogue.Primitives.Point(job.Dest.X, job.Dest.Y);
                        item.Z = job.Dest.Z;
                    }
                    finished.Add(job);
                    Logger.Log($"[HAULJOBS][{tick}] Completed haul item={job.ItemId} to=({job.Dest.X},{job.Dest.Y},{job.Dest.Z}) by worker={job.CreatureId}");
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
}

