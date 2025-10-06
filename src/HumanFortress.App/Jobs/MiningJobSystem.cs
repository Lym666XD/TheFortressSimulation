using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.App.Jobs;

/// <summary>
/// Executes mining jobs: move to adjacency, dig for N ticks, then commit terrain change and drops (TODO).
/// Minimal v1.1.1 skeleton: movement + staged progress + TODO for SetTerrain/AddItem via Diff.
/// </summary>
public sealed class MiningJobSystem : ITick
{
    private readonly HumanFortress.Simulation.World.World _world;
    private readonly MiningSystem _planner;
    private readonly NavigationManager _nav;
    private readonly IPathService _paths;
    private readonly WorldNavigationView _navView;
    private readonly MovementExecutor _move;
    
    private readonly List<ActiveMiningJob> _active = new();
    private readonly List<MiningSystem.PlannedDig> _inboxBuffer = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<MiningSystem.PlannedDig> _backlog = new();
    private readonly HumanFortress.Simulation.Items.ItemsDiffLog? _itemsDiff;
    // Recently completed tiles for short-lived completion highlight
    private readonly System.Collections.Generic.List<(SadRogue.Primitives.Point Cell, int Z, ulong ExpireTick)> _recentCompleted = new();
    private readonly DiffLog? _diff;

    // Tile reservation: prevent multiple workers from mining the same tile
    private readonly System.Collections.Generic.HashSet<(int x, int y, int z)> _reservedTiles = new();

    // Replan/timeout tuning
    private const int MaxFailedReplans = 10; // After N failed replans, release reservation and requeue

    public MiningJobSystem(HumanFortress.Simulation.World.World world, MiningSystem planner, DiffLog? diffLog = null, HumanFortress.Simulation.Items.ItemsDiffLog? itemsDiff = null, NavigationManager? sharedNav = null)
    {
        _world = world;
        _planner = planner;
        _nav = sharedNav ?? new NavigationManager(world);
        _paths = new PathService(NavigationTuning.LoadFromContent());
        _navView = new WorldNavigationView(_nav, world);
        _move = new MovementExecutor(_paths);
        _diff = diffLog;
        _itemsDiff = itemsDiff;
        // Note: RebuildAll is performed after world generation (FortressState) using the shared manager.
    }

    public int Priority => UpdateOrder.Priority.Jobs;
    public string SystemId => "Jobs.MiningJobSystem";
    public NavigationManager NavigationManager => _nav;

    public void ReadTick(ulong tick)
    {
        _paths.BeginTick();
        _inboxBuffer.Clear();
        while (_inboxBuffer.Count < 16 && _backlog.TryDequeue(out var retry))
            _inboxBuffer.Add(retry);
        if (_inboxBuffer.Count < 16)
            _planner.DequeuePlannedDigs(16 - _inboxBuffer.Count, _inboxBuffer);
        if (_inboxBuffer.Count == 0)
        {
            if ((tick % 60UL) == 0UL)
                Logger.Log($"[MINING][{tick}] No planned digs dequeued.");
            return;
        }

        var digs = _inboxBuffer
            .OrderBy(p => p.Cell.Y).ThenBy(p => p.Cell.X).ThenBy(p => p.Z)
            .ToList();
        var creatures = _world.Creatures.GetAllInstances().OrderBy(c => c.Guid).ToList();
        if (digs.Count > 0)
        {
            Logger.Log($"[MINING][{tick}] Planned digs dequeued: {digs.Count}; available workers: {creatures.Count()}");
        }
        var busy = new HashSet<Guid>(_active.Select(a => a.WorkerId));
        foreach (var pd in digs)
        {
            bool assigned = false;

            // Light dependency for stairwell: require a carved stair on a neighboring Z before assigning middle/bottom
            if (pd.Action == HumanFortress.Simulation.Orders.MiningAction.DigStairwell &&
                (pd.Segment == HumanFortress.Simulation.Orders.MiningSegment.Middle || pd.Segment == HumanFortress.Simulation.Orders.MiningSegment.Bottom))
            {
                if (!StairDependencySatisfied(pd.Cell, pd.Z))
                {
                    _backlog.Enqueue(pd);
                    Logger.Log($"[MINING][{tick}] Gate stairwell seg={pd.Segment} at ({pd.Cell.X},{pd.Cell.Y},{pd.Z}) waiting for adjacent layer stair");
                    continue;
                }
            }

            // Skip if tile is already reserved
            var tileKey = (pd.Cell.X, pd.Cell.Y, pd.Z);
            if (_reservedTiles.Contains(tileKey))
            {
                Logger.Log($"[MINING][{tick}] Tile ({pd.Cell.X},{pd.Cell.Y},{pd.Z}) already reserved");
                continue;
            }

            // Skip already-satisfied stairwell segments (idempotent): if target kind already matches, drop PD
            if (pd.Action == HumanFortress.Simulation.Orders.MiningAction.DigStairwell)
            {
                var t0 = _world.GetTile(pd.Cell.X, pd.Cell.Y, pd.Z);
                if (t0 != null)
                {
                    var expected = pd.Segment switch
                    {
                        HumanFortress.Simulation.Orders.MiningSegment.Top => HumanFortress.Simulation.Tiles.TerrainKind.StairsDown,
                        HumanFortress.Simulation.Orders.MiningSegment.Middle => HumanFortress.Simulation.Tiles.TerrainKind.StairsUD,
                        HumanFortress.Simulation.Orders.MiningSegment.Bottom => HumanFortress.Simulation.Tiles.TerrainKind.StairsUp,
                        _ => t0.Value.Kind
                    };
                    if (t0.Value.Kind == expected)
                    {
                        Logger.Log($"[MINING][{tick}] Skip stairwell seg={pd.Segment} already {expected} at ({pd.Cell.X},{pd.Cell.Y},{pd.Z})");
                        continue;
                    }
                }
            }

            // Determine adjacency: for Channel prefer strictly NESW; others allow diagonals and expand radius
            var adj = GetAdjacencyForAction(pd.Action, pd.Cell.X, pd.Cell.Y, pd.Z);
            if (adj == null)
            {
                _backlog.Enqueue(pd);
                Logger.Log($"[MINING][{tick}] No adjacency for target=({pd.Cell.X},{pd.Cell.Y},{pd.Z}); requeue");
                continue;
            }
            foreach (var worker in creatures)
            {
                if (worker.HP <= 0) continue;
                if (busy.Contains(worker.Guid)) continue;
                var req = new PathRequest(new Point3(worker.Position.X, worker.Position.Y, worker.Z), new Point3(adj.Value.X, adj.Value.Y, pd.Z), MoveMode.Walk, PathFlags.AllowDiagonal, SeedFrom(worker.Guid, pd.Cell));
                IWorldNavigationView view = _navView;
                var path = _paths.Solve(in req, in view);
                if (path.Kind != PathResultKind.Found)
                    continue;

                var requiredTicks = CalculateRequiredTicks(pd.GeologyHandle, (HumanFortress.Simulation.Tiles.TerrainKind)pd.TerrainKind);
                var job = new ActiveMiningJob
                {
                    WorkerId = worker.Guid,
                    Target = pd.Cell,
                    Z = pd.Z,
                    Adjacent = adj.Value,
                    Stage = MiningStage.ToAdj,
                    ProgressTicks = 0,
                    RequiredTicks = requiredTicks,
                    GeologyHandle = pd.GeologyHandle,
                    TerrainKind = (HumanFortress.Simulation.Tiles.TerrainKind)pd.TerrainKind,
                    Priority = pd.Priority,
                    AssignedTick = tick,
                    ReplanFailCount = 0,
                    Action = pd.Action,
                    Segment = pd.Segment
                };
                _move.BeginMovement(ToEntity(worker.Guid), req, path);
                _active.Add(job);
                busy.Add(worker.Guid);
                _reservedTiles.Add(tileKey);  // Reserve the tile
                if (pd.Action == HumanFortress.Simulation.Orders.MiningAction.DigChannel && pd.Z > 0)
                    _reservedTiles.Add((pd.Cell.X, pd.Cell.Y, pd.Z - 1));
                Logger.Log($"[MINING][{tick}] Assign worker={worker.Guid} target=({pd.Cell.X},{pd.Cell.Y},{pd.Z}) adj=({adj.Value.X},{adj.Value.Y},{pd.Z}) terrain={job.TerrainKind} ticks={requiredTicks}");
                assigned = true;
                break;
            }
            if (!assigned)
            {
                _backlog.Enqueue(pd);
                Logger.Log($"[MINING][{tick}] No worker for target=({pd.Cell.X},{pd.Cell.Y},{pd.Z})");
            }
        }
    }

    private bool StairDependencySatisfied(SadRogue.Primitives.Point cell, int z)
    {
        bool HasStairAt(int zz)
        {
            var t = _world.GetTile(cell.X, cell.Y, zz);
            if (t == null) return false;
            var k = t.Value.Kind;
            return k == HumanFortress.Simulation.Tiles.TerrainKind.StairsDown ||
                   k == HumanFortress.Simulation.Tiles.TerrainKind.StairsUD ||
                   k == HumanFortress.Simulation.Tiles.TerrainKind.StairsUp;
        }
        bool upOk = (z + 1) < _world.MaxZ && HasStairAt(z + 1);
        bool downOk = (z - 1) >= 0 && HasStairAt(z - 1);
        return upOk || downOk;
    }

    public void WriteTick(ulong tick)
    {
        if (_active.Count == 0) return;
        var finished = new List<ActiveMiningJob>();
        foreach (var job in _active)
        {
            var worker = _world.Creatures.GetInstance(job.WorkerId);
            if (worker == null)
            {
                // Release reservation and requeue if worker disappeared (dead/removed)
                _reservedTiles.Remove((job.Target.X, job.Target.Y, job.Z));
                _backlog.Enqueue(new MiningSystem.PlannedDig(job.Target, job.Z, job.GeologyHandle, (byte)job.TerrainKind, job.Priority, 0UL, job.Action, job.Segment));
                Logger.Log($"[MINING][{tick}] Worker missing; release & requeue target=({job.Target.X},{job.Target.Y},{job.Z})");
                finished.Add(job);
                continue;
            }
            uint eid = ToEntity(worker.Guid);

            if (job.Stage == MiningStage.ToAdj)
            {
                var update = _move.UpdateMovement(eid, _navView);
                if (update.NeedsReplan)
                {
                    var req = new PathRequest(update.Position, new Point3(job.Adjacent.X, job.Adjacent.Y, job.Z), MoveMode.Walk, PathFlags.AllowDiagonal, SeedFrom(job.WorkerId, job.Target));
                    IWorldNavigationView view2 = _navView;
                    var path = _paths.Solve(in req, in view2);
                    if (path.Kind == PathResultKind.Found)
                    {
                        _move.BeginMovement(eid, req, path);
                        Logger.Log($"[MINING][{tick}] Replan worker={job.WorkerId} to adj=({job.Adjacent.X},{job.Adjacent.Y},{job.Z}) kind={path.Kind}");
                    }
                    else
                    {
                        job.ReplanFailCount++;
                        Logger.Log($"[MINING][{tick}] Replan failed kind={path.Kind} worker={job.WorkerId} to adj=({job.Adjacent.X},{job.Adjacent.Y},{job.Z}) fails={job.ReplanFailCount}");
                        if (job.ReplanFailCount >= MaxFailedReplans)
                        {
                            // Timeout: release reservation and requeue
                            _reservedTiles.Remove((job.Target.X, job.Target.Y, job.Z));
                            _backlog.Enqueue(new MiningSystem.PlannedDig(job.Target, job.Z, job.GeologyHandle, (byte)job.TerrainKind, job.Priority, 0UL, job.Action, job.Segment));
                            Logger.Log($"[MINING][{tick}] Release reservation & requeue target=({job.Target.X},{job.Target.Y},{job.Z}) due to timeout (path={path.Kind})");
                            finished.Add(job);
                        }
                    }
                    continue;
                }

                // Emit Diff for creature movement (critical: updates position in world)
                EmitMoveCreatureDiff(eid, update.Position);

                if (update.Status == MovementStatus.Arrived || update.Status == MovementStatus.PathComplete)
                {
                    job.Stage = MiningStage.Digging;
                    Logger.Log($"[MINING][{tick}] Start digging by worker={job.WorkerId} at target=({job.Target.X},{job.Target.Y},{job.Z})");

                    // Stairwell pre-open: when starting to dig a stair segment (Top/Middle/Bottom),
                    // open exactly z-1 as StairsUD if it is SolidWall and within valid z-range.
                    // This ensures vertical path without over-excavating beyond the selected z-range.
                    if (job.Action == HumanFortress.Simulation.Orders.MiningAction.DigStairwell && job.Z > 0)
                    {
                        int bz = job.Z - 1;
                        // Only pre-open if z-1 is NOT the bottom segment (i.e., if this segment is Top or Middle)
                        // and z-1 tile is SolidWall. We don't track zMin/zMax per PD currently,
                        // so we minimally enforce: only pre-open z-1 if it's a wall. This ensures
                        // the worker can descend. A more precise impl would carry zMin/zMax on PD
                        // and check bz >= zMin, but the planner already segments correctly (Top/Middle/Bottom).
                        var below = _world.GetTile(job.Target.X, job.Target.Y, bz);
                        if (below != null && below.Value.Kind == HumanFortress.Simulation.Tiles.TerrainKind.SolidWall)
                        {
                            EmitSetTerrain(job.Target, bz, HumanFortress.Simulation.Tiles.TerrainKind.StairsUD, below.Value.GeoMatId);
                            foreach (var (dropId, qty) in ChooseDropsFor(below.Value.GeoMatId, HumanFortress.Simulation.Tiles.TerrainKind.SolidWall))
                                if (!string.IsNullOrEmpty(dropId) && qty > 0) EmitAddItem(job.Target, bz, dropId, qty);
                            Logger.Log($"[MINING] Stair Pre-open UD at ({job.Target.X},{job.Target.Y},{bz}) (one layer)");
                        }
                    }
                }
                continue;
            }

            if (job.Stage == MiningStage.Digging)
            {
                job.ProgressTicks++;
                if (job.ProgressTicks >= job.RequiredTicks)
                {
                    // Verify target exists then apply action-specific result
                    var verifyTile = _world.GetTile(job.Target.X, job.Target.Y, job.Z);
                    if (verifyTile != null)
                    {
                        // Channel safety: if occupied by a creature OTHER THAN the current worker, requeue instead of carving under feet
                        if (job.Action == HumanFortress.Simulation.Orders.MiningAction.DigChannel && AnyCreatureAtExcept(job.Target, job.Z, job.WorkerId))
                        {
                            _backlog.Enqueue(new MiningSystem.PlannedDig(job.Target, job.Z, job.GeologyHandle, (byte)job.TerrainKind, job.Priority, 0UL, job.Action, job.Segment));
                            Logger.Log($"[MINING][{tick}] Channel target occupied by other creature at ({job.Target.X},{job.Target.Y},{job.Z}); requeue");
                            // Release reservation and skip completion
                            _reservedTiles.Remove((job.Target.X, job.Target.Y, job.Z));
                            if (job.Z > 0) _reservedTiles.Remove((job.Target.X, job.Target.Y, job.Z - 1));
                            finished.Add(job);
                            continue;
                        }
                        job.Stage = MiningStage.Complete;
                        ApplyMiningResult(job);
                        _recentCompleted.Add((job.Target, job.Z, tick + 100)); // short-lived completion highlight
                        Logger.Log($"[MINING][{tick}] Dig complete at target=({job.Target.X},{job.Target.Y},{job.Z}) action={job.Action} by {job.WorkerId}");
                    }
                    else
                    {
                        Logger.Log($"[MINING][{tick}] Tile ({job.Target.X},{job.Target.Y},{job.Z}) changed during dig, aborting");
                    }

                    // Release reservation
                    _reservedTiles.Remove((job.Target.X, job.Target.Y, job.Z));
                    if (job.Action == HumanFortress.Simulation.Orders.MiningAction.DigChannel && job.Z > 0)
                        _reservedTiles.Remove((job.Target.X, job.Target.Y, job.Z - 1));
                    finished.Add(job);
                }
            }
        }
        if (finished.Count > 0)
            foreach (var f in finished) _active.Remove(f);
    }

    private void EmitSetTerrainOpen(SadRogue.Primitives.Point cell, int z)
    {
        if (_diff == null) return;
        int chunkX = cell.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int chunkY = cell.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localX = cell.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localY = cell.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localIndex = localY * HumanFortress.Simulation.World.Chunk.SIZE_XY + localX;
        int chunkId = EncodeChunkId(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, z));
        var target = new DiffTarget(chunkId, localIndex);
        ulong args = (ulong)HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor; // low 8 bits store new kind
        _diff.AddOp(new DiffOp(DiffOpType.SetTerrain, target, SystemId, Priority, args));
    }

    private void EmitSetTerrainKind(SadRogue.Primitives.Point cell, int z, HumanFortress.Simulation.Tiles.TerrainKind kind)
    {
        if (_diff == null) return;
        int chunkX = cell.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int chunkY = cell.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localX = cell.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localY = cell.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localIndex = localY * HumanFortress.Simulation.World.Chunk.SIZE_XY + localX;
        int chunkId = EncodeChunkId(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, z));
        var target = new DiffTarget(chunkId, localIndex);
        ulong args = (ulong)kind;
        _diff.AddOp(new DiffOp(DiffOpType.SetTerrain, target, SystemId, Priority, args));
    }

    private void EmitSetTerrain(SadRogue.Primitives.Point cell, int z, HumanFortress.Simulation.Tiles.TerrainKind kind, ushort overrideGeology)
    {
        if (_diff == null) return;
        int chunkX = cell.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int chunkY = cell.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localX = cell.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localY = cell.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localIndex = localY * HumanFortress.Simulation.World.Chunk.SIZE_XY + localX;
        int chunkId = EncodeChunkId(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, z));
        var target = new DiffTarget(chunkId, localIndex);
        ulong args = (ulong)kind | ((ulong)overrideGeology << 8);
        _diff.AddOp(new DiffOp(DiffOpType.SetTerrain, target, SystemId, Priority, args));
    }

    private void ApplyMiningResult(ActiveMiningJob job)
    {
        var here = _world.GetTile(job.Target.X, job.Target.Y, job.Z);
        HumanFortress.Simulation.Tiles.TerrainKind? kindHere = here != null ? here.Value.Kind : (HumanFortress.Simulation.Tiles.TerrainKind?)null;
        switch (job.Action)
        {
            case HumanFortress.Simulation.Orders.MiningAction.Dig:
                EmitSetTerrain(job.Target, job.Z, HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor, job.GeologyHandle);
                foreach (var (dropId, qty) in ChooseDropsFor(job.GeologyHandle, HumanFortress.Simulation.Tiles.TerrainKind.SolidWall))
                    if (!string.IsNullOrEmpty(dropId) && qty > 0) EmitAddItem(job.Target, job.Z, dropId, qty);
                break;
            case HumanFortress.Simulation.Orders.MiningAction.DigRamp:
                if (kindHere == HumanFortress.Simulation.Tiles.TerrainKind.SolidWall)
                {
                    EmitSetTerrain(job.Target, job.Z, HumanFortress.Simulation.Tiles.TerrainKind.Ramp, job.GeologyHandle);
                    foreach (var (dropId, qty) in ChooseDropsFor(job.GeologyHandle, HumanFortress.Simulation.Tiles.TerrainKind.Ramp))
                        if (!string.IsNullOrEmpty(dropId) && qty > 0) EmitAddItem(job.Target, job.Z, dropId, qty);
                    // If there is a floor directly above, remove it (become OpenNoFloor)
                    if (job.Z + 1 < _world.MaxZ)
                    {
                        var above = _world.GetTile(job.Target.X, job.Target.Y, job.Z + 1);
                        if (above != null && above.Value.Kind == HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor)
                            EmitSetTerrain(job.Target, job.Z + 1, HumanFortress.Simulation.Tiles.TerrainKind.OpenNoFloor, above.Value.GeoMatId);
                    }
                }
                else
                {
                    EmitSetTerrain(job.Target, job.Z, HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor, job.GeologyHandle);
                    foreach (var (dropId, qty) in ChooseDropsFor(job.GeologyHandle, HumanFortress.Simulation.Tiles.TerrainKind.SolidWall))
                        if (!string.IsNullOrEmpty(dropId) && qty > 0) EmitAddItem(job.Target, job.Z, dropId, qty);
                }
                break;
            case HumanFortress.Simulation.Orders.MiningAction.DigChannel:
                // Try override geology to 'air' for open space
                ushort airGeo = 0;
                try
                {
                    var reg = HumanFortress.Core.Content.ContentRegistry.Instance;
                    if (reg.TryGetGeologyHandleByMaterialAndKind("air", HumanFortress.Simulation.Tiles.TerrainKind.OpenNoFloor.ToString(), out var h))
                        airGeo = h;
                }
                catch { }
                EmitSetTerrain(job.Target, job.Z, HumanFortress.Simulation.Tiles.TerrainKind.OpenNoFloor, airGeo);
                if (job.Z > 0)
                {
                    var below = _world.GetTile(job.Target.X, job.Target.Y, job.Z - 1);
                    if (below != null && below.Value.Kind == HumanFortress.Simulation.Tiles.TerrainKind.SolidWall)
                    {
                        EmitSetTerrain(job.Target, job.Z - 1, HumanFortress.Simulation.Tiles.TerrainKind.Ramp, below.Value.GeoMatId);
                        ushort belowGeo = below.Value.GeoMatId;
                        foreach (var (dropId, qty) in ChooseDropsFor(belowGeo, HumanFortress.Simulation.Tiles.TerrainKind.Ramp))
                            if (!string.IsNullOrEmpty(dropId) && qty > 0) EmitAddItem(job.Target, job.Z, dropId, qty);
                    }
                }
                break;
            case HumanFortress.Simulation.Orders.MiningAction.DigStairwell:
                var targetKind = job.Segment switch
                {
                    HumanFortress.Simulation.Orders.MiningSegment.Top => HumanFortress.Simulation.Tiles.TerrainKind.StairsDown,
                    HumanFortress.Simulation.Orders.MiningSegment.Middle => HumanFortress.Simulation.Tiles.TerrainKind.StairsUD,
                    HumanFortress.Simulation.Orders.MiningSegment.Bottom => HumanFortress.Simulation.Tiles.TerrainKind.StairsUp,
                    _ => HumanFortress.Simulation.Tiles.TerrainKind.StairsUD
                };
                EmitSetTerrain(job.Target, job.Z, targetKind, job.GeologyHandle);
                if (kindHere == HumanFortress.Simulation.Tiles.TerrainKind.SolidWall)
                {
                    foreach (var (dropId, qty) in ChooseDropsFor(job.GeologyHandle, HumanFortress.Simulation.Tiles.TerrainKind.SolidWall))
                        if (!string.IsNullOrEmpty(dropId) && qty > 0) EmitAddItem(job.Target, job.Z, dropId, qty);
                }
                break;
            default:
                EmitSetTerrain(job.Target, job.Z, HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor, job.GeologyHandle);
                foreach (var (dropId, qty) in ChooseDropsFor(job.GeologyHandle, HumanFortress.Simulation.Tiles.TerrainKind.SolidWall))
                    if (!string.IsNullOrEmpty(dropId) && qty > 0) EmitAddItem(job.Target, job.Z, dropId, qty);
                break;
        }
    }

    private int CalculateRequiredTicks(ushort geologyHandle, HumanFortress.Simulation.Tiles.TerrainKind terrainKind)
    {
        try
        {
            var reg = HumanFortress.Core.Content.ContentRegistry.Instance;
            var ticksObj = reg.GetTuning<Newtonsoft.Json.Linq.JObject>("tuning.mining", "$.geology_ticks.default");
            if (ticksObj == null) return 20;

            string key = terrainKind == HumanFortress.Simulation.Tiles.TerrainKind.Ramp ? "ramp" : "wall";
            var ticksToken = ticksObj[key];
            var ticks = ticksToken?.ToObject<int?>() ?? 20;
            return Math.Max(1, ticks);
        }
        catch
        {
            return 20;
        }
    }

    private static readonly object _dropsCacheLock = new();
    private static bool _dropsCacheBuilt = false;
    private struct DropDef { public string Id; public int Min; public int Max; public double Weight; }
    private sealed class DropTable { public List<DropDef> Wall = new(); public List<DropDef> Ramp = new(); }
    private static readonly System.Collections.Generic.Dictionary<string, DropTable> _dropsCache = new();

    private static void EnsureDropsCache()
    {
        if (_dropsCacheBuilt) return;
        lock (_dropsCacheLock)
        {
            if (_dropsCacheBuilt) return;
            var reg = HumanFortress.Core.Content.ContentRegistry.Instance;
            var root = reg.GetTuning<Newtonsoft.Json.Linq.JObject>("tuning.mining", "$.geology_drops");
            if (root != null)
            {
                foreach (var prop in root.Properties())
                {
                    var key = prop.Name;
                    var val = prop.Value as Newtonsoft.Json.Linq.JObject;
                    if (val == null) continue;
                    var table = new DropTable();
                    void Fill(string name, List<DropDef> lst)
                    {
                        var arr = val[name] as Newtonsoft.Json.Linq.JArray;
                        if (arr == null) return;
                        foreach (var e in arr)
                        {
                            var id = e["item_id"]?.ToObject<string>();
                            if (string.IsNullOrWhiteSpace(id)) continue;
                            int min = e["min"]?.ToObject<int?>() ?? 1;
                            int max = e["max"]?.ToObject<int?>() ?? min;
                            double w = e["weight"]?.ToObject<double?>() ?? 1.0;
                            lst.Add(new DropDef { Id = id!, Min = min, Max = max, Weight = w });
                        }
                    }
                    Fill("wall", table.Wall);
                    Fill("ramp", table.Ramp);
                    _dropsCache[key] = table;
                    if (key.StartsWith("core_geology_", StringComparison.OrdinalIgnoreCase))
                    {
                        var sfx = key.Substring("core_geology_".Length);
                        _dropsCache["core_terrain_wall_rock_" + sfx] = table;
                        _dropsCache["core_terrain_floor_rock_" + sfx] = table;
                        _dropsCache["core_terrain_wall_ore_" + sfx] = table;
                    }
                }
            }
            _dropsCacheBuilt = true;
        }
    }

    private List<(string itemId, int qty)> ChooseDropsFor(ushort geologyHandle, HumanFortress.Simulation.Tiles.TerrainKind terrainKind)
    {
        var result = new List<(string, int)>();
        try
        {
            var reg = HumanFortress.Core.Content.ContentRegistry.Instance;
            EnsureDropsCache();

            // Get geology ID from handle
            var geology = reg.GetGeologyByHandle(geologyHandle);
            string geoKey = geology != null ? geology.Id : "default";

            // Try cached drop-table first
            _dropsCache.TryGetValue(geoKey, out var cacheTable);
            if (cacheTable == null)
            {
                // Attempt to resolve by normalized geology id (map core_terrain_wall_rock_x -> core_geology_x, likewise for floor/ore)
                if (geology != null)
                {
                    var id = geology.Id;
                    string? norm = null;
                    const string pRockWall = "core_terrain_wall_rock_";
                    const string pRockFloor = "core_terrain_floor_rock_";
                    const string pOreWall = "core_terrain_wall_ore_";
                    if (id.StartsWith(pRockWall, StringComparison.OrdinalIgnoreCase))
                        norm = "core_geology_" + id.Substring(pRockWall.Length);
                    else if (id.StartsWith(pRockFloor, StringComparison.OrdinalIgnoreCase))
                        norm = "core_geology_" + id.Substring(pRockFloor.Length);
                    else if (id.StartsWith(pOreWall, StringComparison.OrdinalIgnoreCase))
                        norm = "core_geology_" + id.Substring(pOreWall.Length);

                    if (!string.IsNullOrEmpty(norm)) _dropsCache.TryGetValue(norm, out cacheTable);
                }

                if (cacheTable == null) _dropsCache.TryGetValue("default", out cacheTable);
            }
            if (cacheTable != null)
            {
                var list = terrainKind == HumanFortress.Simulation.Tiles.TerrainKind.Ramp ? cacheTable.Ramp : cacheTable.Wall;
                if (list.Count > 0)
                {
                    var seed = (uint)geologyHandle ^ (uint)terrainKind;
                    var rng = new System.Random((int)seed);
                    foreach (var d in list)
                    {
                        int q = rng.Next(d.Min, d.Max + 1);
                        if (q > 0) result.Add((d.Id, q));
                    }
                    return result;
                }
            }

            // Legacy JSON lookup fallback (should rarely happen now)
            var dropsObj = reg.GetTuning<Newtonsoft.Json.Linq.JObject>("tuning.mining", $"$.geology_drops.{geoKey}");
            if (dropsObj == null)
            {
                dropsObj = reg.GetTuning<Newtonsoft.Json.Linq.JObject>("tuning.mining", "$.geology_drops.default");
            }
            if (dropsObj == null) return result;

            string key = terrainKind == HumanFortress.Simulation.Tiles.TerrainKind.Ramp ? "ramp" : "wall";
            var dropsList = dropsObj[key] as Newtonsoft.Json.Linq.JArray;
            if (dropsList == null)
            {
                // As a last resort, synthesize a simple mapping from geology id -> item id
                if (geology != null)
                {
                    var id = geology.Id;
                    const string pRockWall = "core_terrain_wall_rock_";
                    const string pOreWall = "core_terrain_wall_ore_";
                    if (id.StartsWith(pRockWall, StringComparison.OrdinalIgnoreCase))
                    {
                        var rock = id.Substring(pRockWall.Length);
                        string itemId = $"core_item_boulder_{rock}";
                        int qty = terrainKind == HumanFortress.Simulation.Tiles.TerrainKind.Ramp ? 1 : 3;
                        result.Add((itemId, qty));
                        return result;
                    }
                    if (id.StartsWith(pOreWall, StringComparison.OrdinalIgnoreCase))
                    {
                        var ore = id.Substring(pOreWall.Length);
                        string itemId = $"core_item_ore_{ore}";
                        int qty = terrainKind == HumanFortress.Simulation.Tiles.TerrainKind.Ramp ? 1 : 2;
                        result.Add((itemId, qty));
                        return result;
                    }
                }
                return result;
            }

            foreach (var dropEntry in dropsList)
            {
                var itemIdToken = dropEntry["item_id"];
                var itemId = itemIdToken?.ToObject<string>();
                if (string.IsNullOrEmpty(itemId)) continue;

                var minToken = dropEntry["min"];
                var maxToken = dropEntry["max"];
                var min = minToken?.ToObject<int?>() ?? 1;
                var max = maxToken?.ToObject<int?>() ?? min;

                // Deterministic RNG based on geology + terrain
                var seed = (uint)geologyHandle ^ (uint)terrainKind;
                var rng = new System.Random((int)seed);
                var qty = rng.Next(min, max + 1);

                result.Add((itemId, qty));
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.Log($"[MINING] Failed to load drops: {ex.Message}");
            return new List<(string, int)> { ("core_item_boulder_granite", 1) };
        }
    }

    private void EmitAddItem(SadRogue.Primitives.Point cell, int z, string itemId, int quantity)
    {
        if (_itemsDiff == null) return;
        int chunkX = cell.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int chunkY = cell.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localX = cell.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localY = cell.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localIndex = localY * HumanFortress.Simulation.World.Chunk.SIZE_XY + localX;
        var ck = new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, z);
        _itemsDiff.Add(HumanFortress.Simulation.Items.ItemsDiffOp.AddItem, ck, localIndex, itemId, quantity, Priority, SystemId);
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

    private void PrecarveStairChain(SadRogue.Primitives.Point cell, int zStart, ulong tick)
    {
        int bz = zStart - 1;
        while (bz >= 0)
        {
            var below = _world.GetTile(cell.X, cell.Y, bz);
            if (below == null) break;
            if (below.Value.Kind != HumanFortress.Simulation.Tiles.TerrainKind.SolidWall) break;
            EmitSetTerrainKind(cell, bz, HumanFortress.Simulation.Tiles.TerrainKind.StairsUD);
            foreach (var (dropId, qty) in ChooseDropsFor(below.Value.GeoMatId, HumanFortress.Simulation.Tiles.TerrainKind.SolidWall))
                if (!string.IsNullOrEmpty(dropId) && qty > 0) EmitAddItem(cell, zStart, dropId, qty);
            Logger.Log($"[MINING][{tick}] Precarve stair UD at ({cell.X},{cell.Y},{bz}) for stairwell chain");
            bz--;
        }
    }

    public System.Collections.Generic.List<(SadRogue.Primitives.Point Cell, int Z)> GetRecentCompletions(ulong now)
    {
        for (int i = _recentCompleted.Count - 1; i >= 0; i--)
        {
            if (_recentCompleted[i].ExpireTick <= now) _recentCompleted.RemoveAt(i);
        }
        return _recentCompleted.Select(rc => (rc.Cell, rc.Z)).ToList();
    }

    private bool AnyCreatureAt(SadRogue.Primitives.Point cell, int z)
    {
        foreach (var c in _world.Creatures.GetAllInstances())
        {
            if (c.Z == z && c.Position.X == cell.X && c.Position.Y == cell.Y)
                return true;
        }
        return false;
    }

    private bool AnyCreatureAtExcept(SadRogue.Primitives.Point cell, int z, System.Guid exceptId)
    {
        foreach (var c in _world.Creatures.GetAllInstances())
        {
            if (c.Z == z && c.Position.X == cell.X && c.Position.Y == cell.Y && c.Guid != exceptId)
                return true;
        }
        return false;
    }

    private (int X,int Y)? GetAdjacencyForAction(HumanFortress.Simulation.Orders.MiningAction action, int x, int y, int z)
    {
        // Prefer NESW around target if walkable; then diagonals; then expand radius up to 3 using BFS-like rings.
        static IEnumerable<(int dx,int dy)> Ortho() { yield return (0,-1); yield return (1,0); yield return (0,1); yield return (-1,0); }
        static IEnumerable<(int dx,int dy)> Diag() { yield return (1,-1); yield return (1,1); yield return (-1,1); yield return (-1,-1); }

        // Special-case: Stairwell top can be dug while standing on the target if it's a floor
        if (action == HumanFortress.Simulation.Orders.MiningAction.DigStairwell || action == HumanFortress.Simulation.Orders.MiningAction.DigChannel)
        {
            var self = _world.GetTile(x, y, z);
            if (self != null && self.Value.IsStandable)
            {
                return (x, y);
            }
        }

        bool Acceptable(int tx, int ty)
        {
            var t = _world.GetTile(tx, ty, z);
            return t != null && t.Value.IsWalkable; // allow floors, ramps, stairs as adjacency
        }

        foreach (var (dx, dy) in Ortho()) if (Acceptable(x + dx, y + dy)) return (x + dx, y + dy);
        if (action != HumanFortress.Simulation.Orders.MiningAction.DigChannel)
            foreach (var (dx, dy) in Diag()) if (Acceptable(x + dx, y + dy)) return (x + dx, y + dy);

        for (int r = 2; r <= 3; r++)
        {
            for (int yy = y - r; yy <= y + r; yy++)
            {
                int xx1 = x - r; int xx2 = x + r;
                if (Acceptable(xx1, yy)) return (xx1, yy);
                if (Acceptable(xx2, yy)) return (xx2, yy);
            }
            for (int xx = x - r + 1; xx <= x + r - 1; xx++)
            {
                int yy1 = y - r; int yy2 = y + r;
                if (Acceptable(xx, yy1)) return (xx, yy1);
                if (Acceptable(xx, yy2)) return (xx, yy2);
            }
        }
        return null;
    }

    private static uint ToEntity(Guid g)
    {
        var bytes = g.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static uint SeedFrom(Guid a, Point b)
    {
        unchecked
        {
            var ba = a.ToByteArray();
            uint s = 2166136261;
            foreach (var t in ba) s = (s ^ t) * 16777619;
            s = (s ^ (uint)b.X) * 16777619;
            s = (s ^ (uint)b.Y) * 16777619;
            return s;
        }
    }

    private enum MiningStage { ToAdj, Digging, Complete }

    private sealed class ActiveMiningJob
    {
        public Guid WorkerId { get; init; }
        public SadRogue.Primitives.Point Target { get; init; }
        public int Z { get; init; }
        public SadRogue.Primitives.Point Adjacent { get; set; }
        public MiningStage Stage { get; set; }
        public int ProgressTicks { get; set; }
        public int RequiredTicks { get; set; }
        public ushort GeologyHandle { get; init; }
        public HumanFortress.Simulation.Tiles.TerrainKind TerrainKind { get; init; }
        public int Priority { get; init; }
        public ulong AssignedTick { get; init; }
        public int ReplanFailCount { get; set; }
        public HumanFortress.Simulation.Orders.MiningAction Action { get; init; }
        public HumanFortress.Simulation.Orders.MiningSegment Segment { get; init; }
    }

    public readonly record struct ActiveMiningJobView(
        Guid WorkerId,
        SadRogue.Primitives.Point Target,
        int Z,
        SadRogue.Primitives.Point Adjacent,
        string Stage,
        int ProgressTicks,
        int RequiredTicks);

    public List<ActiveMiningJobView> GetActiveJobsSnapshot()
    {
        var list = new List<ActiveMiningJobView>(_active.Count);
        foreach (var j in _active)
        {
            list.Add(new ActiveMiningJobView(
                j.WorkerId,
                j.Target,
                j.Z,
                j.Adjacent,
                j.Stage.ToString(),
                j.ProgressTicks,
                j.RequiredTicks));
        }
        return list;
    }

    private static int EncodeChunkId(HumanFortress.Simulation.World.ChunkKey ck)
    {
        int x = ck.ChunkX & 0x3FF;
        int y = ck.ChunkY & 0x3FF;
        int z = ck.Z & 0x3FF;
        return (z << 20) | (x << 10) | y;
    }
}
