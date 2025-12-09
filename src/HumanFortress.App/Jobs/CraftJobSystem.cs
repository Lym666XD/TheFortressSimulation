using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;
using Point3 = HumanFortress.Navigation.Point3;

namespace HumanFortress.App.Jobs;

/// <summary>
/// Executes craft jobs at workshops: move worker to anchor, wait duration, spawn outputs.
/// </summary>
public sealed class CraftJobSystem : ITick
{
    private readonly World _world;
    private readonly CraftPlanner _planner;
    private readonly DiffLog? _diff;
    private readonly NavigationManager _nav;
    private readonly IPathService _paths;
    private readonly WorldNavigationView _navView;
    private readonly IWorldNavigationView _navViewInterface;
    private readonly MovementExecutor _move;
    private readonly ProfessionAssignments? _professions;
    private readonly WorkerSelectionStrategy _workerStrategy;

    private readonly List<PlannedCraftJob> _inbox = new();
    private readonly ConcurrentQueue<PlannedCraftJob> _backlog = new();
    private readonly List<ActiveCraftJob> _active = new();

    private const int CreatureReserveTtlTicks = 200;
    private int _lastCompleted;
    private int _completedTotal;
    public int LastIntakeCount { get; private set; }

    private CraftJobStatsSnapshot _lastStats;

    public CraftJobSystem(
        World world,
        CraftPlanner planner,
        DiffLog? diffLog,
        NavigationManager? sharedNav,
        ProfessionAssignments? professions,
        WorkerSelectionStrategy workerStrategy)
    {
        _world = world;
        _planner = planner;
        _diff = diffLog;
        _nav = sharedNav ?? new NavigationManager(world);
        _paths = new PathService(NavigationTuning.LoadFromContent());
        _navView = new WorldNavigationView(_nav, world);
        _navViewInterface = _navView;
        _move = new MovementExecutor(_paths);
        _professions = professions;
        _workerStrategy = workerStrategy;
    }

    public int Priority => UpdateOrder.Priority.Jobs;
    public string SystemId => "Jobs.Craft";

    public void ReadTick(ulong tick)
    {
        _paths.BeginTick();
        _inbox.Clear();

        while (_backlog.TryDequeue(out var pending))
            _inbox.Add(pending);

        _planner.DequeuePlannedJobs(32, _inbox);
        LastIntakeCount = _inbox.Count;
        if (_inbox.Count == 0)
        {
            _lastStats = new CraftJobStatsSnapshot(0, _active.Count, _backlog.Count, 0);
            return;
        }

        var workers = _world.Creatures.GetAllInstances().OrderBy(c => c.Guid).ToList();
        var busy = new HashSet<Guid>(_active.Select(j => j.WorkerId));

        foreach (var job in _inbox)
        {
            if (!TryFindWorkshop(job.WorkshopGuid, out var placeable, out var state))
            {
                continue;
            }
            if (state.Queue.Count == 0)
                continue;
            var entry = state.GetEntry(job.QueueEntryId);
            if (entry == null)
                continue;
            entry.IsScheduled = false;

            var recipe = RecipeRegistry.Instance.GetRecipe(job.RecipeId);
            if (recipe == null)
                continue;

            var candidates = _professions?.SelectCandidates(_world, recipe.JobTag, _workerStrategy, busy, _world.Reservations, new Point3(job.Anchor.X, job.Anchor.Y, job.Z))
                ?? workers;

            Guid? assigned = null;
            foreach (var worker in candidates)
            {
                if (busy.Contains(worker.Guid)) continue;
                if (!_world.Reservations.TryReserveCreature(worker.Guid, SystemId, tick + (ulong)CreatureReserveTtlTicks, jobId: $"craft:{job.RecipeId}"))
                    continue;

                var start = new Point3(worker.Position.X, worker.Position.Y, worker.Z);
                var dest = new Point3(job.Anchor.X, job.Anchor.Y, job.Z);
                var req = new PathRequest(start, dest, MoveMode.Walk, PathFlags.None, SeedFrom(worker.Guid, job.WorkshopGuid));
                var path = _paths.Solve(in req, in _navViewInterface);
                if (path.Kind != PathResultKind.Found)
                {
                    _world.Reservations.ReleaseCreature(worker.Guid);
                    continue;
                }

                uint eid = ToEntity(worker.Guid);
                _move.BeginMovement(eid, req, path);

                var activeJob = new ActiveCraftJob
                {
                    WorkerId = worker.Guid,
                    WorkshopGuid = job.WorkshopGuid,
                    QueueEntryId = job.QueueEntryId,
                    RecipeId = job.RecipeId,
                    Stage = CraftJobStage.ToWorkshop,
                    WorkTicksRemaining = job.DurationTicks,
                    Anchor = job.Anchor,
                    Z = job.Z
                };
                _active.Add(activeJob);
                busy.Add(worker.Guid);
                state.RegisterJobStart();
                entry.Status = CraftQueueStatus.InProgress;
                entry.ActiveWorkerId = worker.Guid;
                assigned = worker.Guid;
                break;
            }

            if (!assigned.HasValue)
            {
                _backlog.Enqueue(job);
            }
        }

        _lastStats = new CraftJobStatsSnapshot(LastIntakeCount, _active.Count, _backlog.Count, 0);
    }

    public void WriteTick(ulong tick)
    {
        if (_active.Count == 0) return;
        var finished = new List<ActiveCraftJob>();
        foreach (var job in _active)
        {
            var worker = _world.Creatures.GetInstance(job.WorkerId);
            if (worker == null)
            {
                finished.Add(job);
                _world.Reservations.ReleaseCreature(job.WorkerId);
                continue;
            }

            uint eid = ToEntity(job.WorkerId);
            _world.Reservations.TryReserveCreature(job.WorkerId, SystemId, tick + (ulong)CreatureReserveTtlTicks, jobId: $"craft:{job.RecipeId}");

            if (job.Stage == CraftJobStage.ToWorkshop)
            {
                var update = _move.UpdateMovement(eid, _navView);
                if (update.Status == MovementStatus.Arrived)
                {
                    if (!TryConsumeInputs(job))
                    {
                        finished.Add(job);
                        continue;
                    }
                    job.Stage = CraftJobStage.Working;
                }
                else if (update.NeedsReplan)
                {
                    var req = new PathRequest(update.Position, new Point3(job.Anchor.X, job.Anchor.Y, job.Z), MoveMode.Walk, PathFlags.None, SeedFrom(job.WorkerId, job.WorkshopGuid));
                    var path = _paths.Solve(in req, in _navViewInterface);
                    if (path.Kind == PathResultKind.Found)
                        _move.BeginMovement(eid, req, path);
                }
                continue;
            }

            if (job.Stage == CraftJobStage.Working)
            {
                job.WorkTicksRemaining = Math.Max(0, job.WorkTicksRemaining - 1);
                if (job.WorkTicksRemaining <= 0)
                {
                    SpawnOutputs(job);
                    finished.Add(job);
                    _completedTotal++;
                }
            }
        }

        foreach (var job in finished)
        {
            _world.Reservations.ReleaseCreature(job.WorkerId);
            CleanupJob(job);
            _active.Remove(job);
        }

        _lastStats = new CraftJobStatsSnapshot(LastIntakeCount, _active.Count, _backlog.Count, _completedTotal - _lastCompleted);
        _lastCompleted = _completedTotal;
    }

    private bool TryConsumeInputs(ActiveCraftJob job)
    {
        if (!TryFindWorkshop(job.WorkshopGuid, out var placeable, out var state) || placeable == null || state == null)
            return false;
        var entry = state.GetEntry(job.QueueEntryId);
        if (entry == null) return false;
        var recipe = RecipeRegistry.Instance.GetRecipe(job.RecipeId);
        if (recipe == null) return false;
        foreach (var ingredient in recipe.Inputs)
        {
            int remaining = ingredient.Count;
            foreach (var item in _world.Items.GetAllInstances().ToList())
            {
                if (remaining <= 0) break;
                if (item.DefinitionId != ingredient.DefId) continue;
                if (item.Z != job.Z) continue;
                if (!IsOnWorkshop(placeable, item.Position.X, item.Position.Y)) continue;
                int take = Math.Min(item.StackCount, remaining);
                if (take <= 0) continue;
                item.StackCount -= take;
                remaining -= take;
                if (item.StackCount <= 0)
                    _world.Items.RemoveInstance(item.Guid);
            }
            if (remaining > 0)
            {
                entry.Status = CraftQueueStatus.AwaitingMaterials;
                entry.BlockingReason = $"Need {remaining}x {ingredient.DefId}";
                state!.RegisterJobComplete();
                entry.ActiveWorkerId = null;
                return false;
            }
        }
        entry.BlockingReason = null;
        return true;
    }

    private void SpawnOutputs(ActiveCraftJob job)
    {
        var recipe = RecipeRegistry.Instance.GetRecipe(job.RecipeId);
        if (recipe == null) return;
        foreach (var output in recipe.Outputs)
        {
            _world.Items.SpawnItem(output.DefId, job.Anchor, job.Z, output.Count, 0);
        }
    }

    private void CleanupJob(ActiveCraftJob job)
    {
        if (!TryFindWorkshop(job.WorkshopGuid, out var placeable, out var state) || state == null)
            return;
        state.RegisterJobComplete();
        var entry = state.GetEntry(job.QueueEntryId);
        if (entry != null)
        {
            state.RemoveEntry(entry.EntryId);
        }
    }

    public IReadOnlyList<ActiveCraftJobView> GetActiveJobsSnapshot()
    {
        var list = new List<ActiveCraftJobView>(_active.Count);
        foreach (var job in _active)
        {
            list.Add(new ActiveCraftJobView(job.WorkerId, job.WorkshopGuid, job.RecipeId, job.Stage.ToString(), job.WorkTicksRemaining));
        }
        return list;
    }

    public CraftJobStatsSnapshot GetLastStatsSnapshot() => _lastStats;

    private bool TryFindWorkshop(Guid guid, out PlaceableInstance? placeable, out WorkshopState? state)
    {
        placeable = null;
        state = null;
        foreach (var chunk in _world.GetAllChunks())
        {
            var pd = chunk.GetPlaceableData();
            if (pd == null) continue;
            foreach (var p in pd.GetAllOwnedPlaceables())
            {
                if (p.Guid != guid) continue;
                placeable = p;
                state = p.Workshop;
                return state != null;
            }
        }
        return false;
    }

    private static bool IsOnWorkshop(PlaceableInstance placeable, int x, int y)
    {
        var fp = placeable.Footprint;
        return x >= placeable.Position.X && x < placeable.Position.X + fp.W
               && y >= placeable.Position.Y && y < placeable.Position.Y + fp.D;
    }

    private static uint ToEntity(Guid guid)
    {
        var bytes = guid.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static uint SeedFrom(Guid a, Guid b)
    {
        unchecked
        {
            var aa = a.ToByteArray();
            var bb = b.ToByteArray();
            uint hash = 2166136261;
            foreach (var t in aa) hash = (hash ^ t) * 16777619;
            foreach (var t in bb) hash = (hash ^ t) * 16777619;
            return hash;
        }
    }

    private sealed class ActiveCraftJob
    {
        public Guid WorkerId { get; set; }
        public Guid WorkshopGuid { get; set; }
        public Guid QueueEntryId { get; set; }
        public string RecipeId { get; set; } = string.Empty;
        public CraftJobStage Stage { get; set; }
        public int WorkTicksRemaining { get; set; }
        public SadRogue.Primitives.Point Anchor { get; set; }
        public int Z { get; set; }
    }

    private enum CraftJobStage
    {
        ToWorkshop,
        Working
    }
}

public readonly record struct CraftJobStatsSnapshot(int Intake, int Active, int Backlog, int CompletedDelta);

public readonly record struct ActiveCraftJobView(Guid WorkerId, Guid WorkshopGuid, string RecipeId, string Stage, int RemainingTicks);


