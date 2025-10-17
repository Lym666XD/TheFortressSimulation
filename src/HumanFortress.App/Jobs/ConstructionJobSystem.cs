using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Placeables;
using SadRogue.Primitives;

namespace HumanFortress.App.Jobs;

/// <summary>
/// Executes structural construction (L0) by advancing construction sites, consuming delivered materials,
/// emitting SetTerrain when complete, and removing sites.
/// </summary>
public sealed class ConstructionJobSystem : ITick
{
    private readonly HumanFortress.Simulation.World.World _world;
    private readonly DiffLog? _diff;
    private readonly int _maxPerTick;
    public int LastProcessedSites { get; private set; } = 0;
    public int LastIntakeCount { get; private set; } = 0; // legacy field used by orchestrator (repurposed)

    private readonly HumanFortress.Core.Content.Registry.ConstructionTuning _tuning;

    // UI notification hook (set by UI layer). Invoked on L2 completion.
    public static System.Action<int,int,int, SadRogue.Primitives.Rectangle, string, ulong>? UiNotifyWorkshopComplete;

    public ConstructionJobSystem(HumanFortress.Simulation.World.World world, ConstructionSystem planner, DiffLog? diffLog = null, int maxPerTick = 64)
    {
        _world = world;
        _diff = diffLog;
        _maxPerTick = Math.Max(1, maxPerTick);
        _tuning = HumanFortress.Core.Content.Registry.ConstructionTuning.LoadFromContent();
    }

    public int Priority => UpdateOrder.Priority.WorldTerrain;
    public string SystemId => "Jobs.Construction";

    public void ReadTick(ulong tick)
    {
        // No-op: executor reads sites directly from world placeables.
    }

    public void WriteTick(ulong tick)
    {
        if (_diff == null) return;

        int processed = 0;
        foreach (var chunk in _world.GetAllChunks())
        {
            var pd = chunk.GetPlaceableData();
            if (pd == null) continue;
            foreach (var p in pd.GetAllOwnedPlaceables())
            {
                if (processed >= _maxPerTick) break;
                if (p.ConstructionSite == null) continue;
                var site = p.ConstructionSite;

                // Recompute delivered counts on footprint and adjacent ring
                var delivered = CountDeliveredOnFootprintOrRing(p);
                site.MaterialsDelivered = delivered;

                // Log progress
                Logger.Log($"[BUILD.EXEC] site=({p.Position.X},{p.Position.Y},{p.Z}) delivered={FormatDict(delivered)} req={FormatDict(site.MaterialsRequired)} progress={site.BuildProgressTicks}/{site.TotalBuildTicks}");

                bool ready = true;
                foreach (var kv in site.MaterialsRequired)
                {
                    var tag = kv.Key;
                    var need = kv.Value;
                    if (delivered.GetValueOrDefault(tag, 0) < need) { ready = false; break; }
                }

                if (!ready)
                {
                    continue;
                }

                // Advance progress using tuning
                int rate = System.Math.Max(1, _tuning.BuildRateTicks);
                site.BuildProgressTicks = Math.Min(site.TotalBuildTicks, site.BuildProgressTicks + rate);

                if (site.BuildProgressTicks >= site.TotalBuildTicks)
                {
                    // Before completing, ensure no creature occupies the site anchor; attempt gentle relocation.
                    // Perform relocate+complete atomically to avoid a race where the anchor is re-occupied before the next tick.
                    if (IsOccupiedByCreature(p))
                    {
                        bool moved = TryRelocateCreaturesOffSiteSafe(p);
                        Logger.Log($"[BUILD.EXEC] waiting: occupied site=({p.Position.X},{p.Position.Y},{p.Z}) moved={moved}");
                        // Re-check occupancy after relocation; if still occupied, defer this tick
                        if (IsOccupiedByCreature(p))
                            continue;
                    }
                    // Consume required materials from footprint
                    var toConsume = new Dictionary<string, int>(site.MaterialsRequired, StringComparer.OrdinalIgnoreCase);
                    ConsumeMaterialsOnFootprintOrRing(p, toConsume, tick);

                    // After consumption, move any residual items off the anchor cell to a safe nearby cell
                    TryMoveResidualItemsOffAnchor(p, tick);

                    // Branch: L0 terrain vs L2 placeable
                    if (site.TargetId.StartsWith("l0:", System.StringComparison.OrdinalIgnoreCase))
                    {
                        // Emit L0 SetTerrain
                        var kind = MapTargetIdToKind(site.TargetId, p.Z);
                        int chunkX = p.Position.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
                        int chunkY = p.Position.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
                        int lx = p.Position.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
                        int ly = p.Position.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
                        int localIndex = HumanFortress.Simulation.World.Chunk.LocalIndex(lx, ly);
                        int chunkId = EncodeChunkId(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, p.Z));
                        var target = new DiffTarget(chunkId, localIndex);
                        // Allow applicator to derive geology from current material (override=0)
                        ulong args = ConstructionSystem.PackSetTerrainArgs(kind, 0);
                        _diff.AddOp(new DiffOp(DiffOpType.SetTerrain, target, SystemId, UpdateOrder.Priority.WorldTerrain, args));

                        Logger.Log($"[BUILD.COMPLETE] site=({p.Position.X},{p.Position.Y},{p.Z}) kind={kind} consumed={FormatDict(site.MaterialsRequired)} set-terrain");
                    }
                    else
                    {
                        // L2 placeable from ConstructionDefinition
                        var def = HumanFortress.Core.Content.Registry.ConstructionRegistry.Instance.GetConstruction(site.TargetId);
                        if (def != null)
                        {
                            // Important: remove the site FIRST, then place the final workshop.
                            // If we place first and then call RemoveOwnedAt(anchor), we might remove the newly placed instance
                            // because the owned-slot is keyed by anchor cell.
                            try { PlaceableManager.RemoveOwnedAt(_world, p.Position, p.Z, tick); } catch { }

                            var inst = HumanFortress.Simulation.Placeables.PlaceableInstance.CreateFromConstruction(def, p.Position, p.Z, tick);
                            PlaceableManager.PlacePlaceable(_world, inst, tick);
                            Logger.Log($"[BUILD.COMPLETE] workshop id={def.Id} pos=({p.Position.X},{p.Position.Y},{p.Z}) footprint={def.PlaceableProfile.Footprint.W}x{def.PlaceableProfile.Footprint.D}");

                            // Notify UI (toast + flash highlight)
                            var rect = new SadRogue.Primitives.Rectangle(p.Position.X, p.Position.Y, def.PlaceableProfile.Footprint.W, def.PlaceableProfile.Footprint.D);
                            UiNotifyWorkshopComplete?.Invoke(p.Position.X, p.Position.Y, p.Z, rect, def.Id, tick);
                        }
                        else
                        {
                            Logger.Log($"[BUILD.COMPLETE] ERROR: unknown construction id='{site.TargetId}'");
                        }
                    }

                    // Post-completion safety: if any creature still overlaps the anchor (extreme race), relocate once more
                    if (IsOccupiedByCreature(p))
                    {
                        TryRelocateCreaturesOffSiteSafe(p);
                    }

                    // Site removed earlier when placing L2 placeable (or below for L0 branch)

                    processed++;
                }
            }
            if (processed >= _maxPerTick) break;
        }
        LastProcessedSites = processed;
        LastIntakeCount = processed;
    }

    private static int EncodeChunkId(HumanFortress.Simulation.World.ChunkKey ck)
    {
        // [z:10][x:10][y:10]
        return ((ck.Z & 0x3FF) << 20) | ((ck.ChunkX & 0x3FF) << 10) | (ck.ChunkY & 0x3FF);
    }

    private Dictionary<string, int> CountDeliveredOnFootprint(PlaceableInstance site)
    {
        // legacy method unused after ring expansion
        return CountDeliveredOnFootprintOrRing(site);
    }

    private void ConsumeMaterialsOnFootprintOrRing(PlaceableInstance site, Dictionary<string, int> toConsume, ulong tick)
    {
        foreach (var cell in EnumerateFootprintAndRing(site))
        {
            foreach (var it in _world.Items.GetAllInstances().ToList())
            {
                if (it.IsCarried) continue;
                if (it.Position.X != cell.X || it.Position.Y != cell.Y || it.Z != site.Z) continue;
                var def = _world.Items.GetDefinition(it.DefinitionId);
                if (def == null || def.Tags == null) continue;

                foreach (var kv in toConsume.Keys.OrderBy(k => k).ToList())
                {
                    if (toConsume[kv] <= 0) continue;
                    if (!MatchesRequirement(def.Tags, kv)) continue;
                    int take = Math.Min(it.StackCount, toConsume[kv]);
                    if (take <= 0) continue;
                    it.StackCount -= take;
                    toConsume[kv] -= take;
                    if (it.StackCount <= 0)
                    {
                        _world.Items.RemoveInstance(it.Guid);
                    }
                    if (toConsume[kv] <= 0) break;
                }
            }
        }
    }

    private Dictionary<string, int> CountDeliveredOnFootprintOrRing(PlaceableInstance site)
    {
        var delivered = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in EnumerateFootprintAndRing(site))
        {
            foreach (var it in _world.Items.GetAllInstances())
            {
                if (it.IsCarried) continue;
                if (it.Position.X != cell.X || it.Position.Y != cell.Y || it.Z != site.Z) continue;
                var def = _world.Items.GetDefinition(it.DefinitionId);
                if (def == null || def.Tags == null) continue;
                foreach (var req in site.ConstructionSite!.MaterialsRequired.Keys)
                {
                    if (MatchesRequirement(def.Tags, req))
                    {
                        delivered[req] = delivered.GetValueOrDefault(req, 0) + it.StackCount;
                        break;
                    }
                }
            }
        }
        return delivered;
    }

    private IEnumerable<Point> EnumerateFootprintAndRing(PlaceableInstance site)
    {
        var seen = new HashSet<(int,int)>();
        var fp = site.Footprint;
        for (int dy = 0; dy < fp.D; dy++)
        for (int dx = 0; dx < fp.W; dx++)
        {
            int wx = site.Position.X + dx;
            int wy = site.Position.Y + dy;
            if (seen.Add((wx, wy))) yield return new Point(wx, wy);
        }
        var dirs = new (int dx,int dy)[] { (1,0), (-1,0), (0,1), (0,-1) };
        for (int dy = 0; dy < fp.D; dy++)
        for (int dx = 0; dx < fp.W; dx++)
        {
            int wx = site.Position.X + dx;
            int wy = site.Position.Y + dy;
            foreach (var (adx, ady) in dirs)
            {
                int nx = wx + adx;
                int ny = wy + ady;
                if (!_world.IsValidPosition(nx, ny, site.Z)) continue;
                if (seen.Add((nx, ny))) yield return new Point(nx, ny);
            }
        }
    }

    private bool IsOccupiedByCreature(PlaceableInstance site)
    {
        return _world.Creatures.GetAllInstances()
            .Any(cr => cr.Z == site.Z && cr.Position.X == site.Position.X && cr.Position.Y == site.Position.Y);
    }

    private bool TryRelocateCreaturesOffSiteSafe(PlaceableInstance site)
    {
        bool anyMoved = false;
        // Collect occupants currently on anchor
        var occupants = _world.Creatures.GetAllInstances()
            .Where(cr => cr.Z == site.Z && cr.Position.X == site.Position.X && cr.Position.Y == site.Position.Y)
            .ToList();
        if (occupants.Count == 0) return false;

        // Search a small radius (rings distance 1..2) for a safe standable cell that is not a construction site anchor
        var visited = new HashSet<(int,int)>();
        var q = new Queue<(int x,int y,int d)>();
        void Enq(int x,int y,int d){ if (!_world.IsValidPosition(x,y,site.Z)) return; if (visited.Add((x,y))) q.Enqueue((x,y,d)); }
        // seed ring1 around anchor
        var dirs = new (int dx,int dy)[]{ (1,0),(-1,0),(0,1),(0,-1) };
        foreach (var (dx,dy) in dirs) Enq(site.Position.X+dx, site.Position.Y+dy, 1);
        // expand ring2
        foreach (var (dx,dy) in new (int,int)[]{ (2,0),(-2,0),(0,2),(0,-2),(1,1),(1,-1),(-1,1),(-1,-1) }) Enq(site.Position.X+dx, site.Position.Y+dy, 2);

        Point? safe = null;
        while (q.Count > 0)
        {
            var (x,y,d) = q.Dequeue();
            if (d > 2) break;
            var tile = _world.GetTile(x,y,site.Z);
            if (tile == null) continue;
            if (!(tile.Value.IsStandable || tile.Value.IsWalkable)) continue;
            // Exclude cells that are anchors of construction sites
            int cx = x / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int cy = y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int lx = x % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int ly = y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            var ck = new HumanFortress.Simulation.World.ChunkKey(cx, cy, site.Z);
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
            safe = new Point(x,y);
            break;
        }

        if (!safe.HasValue) return false;

        foreach (var cr in occupants)
        {
            EmitMoveCreature(cr.Guid, new HumanFortress.Navigation.Point3(safe.Value.X, safe.Value.Y, site.Z));
            anyMoved = true;
        }
        return anyMoved;
    }

    private void TryMoveResidualItemsOffAnchor(PlaceableInstance site, ulong tick)
    {
        var items = _world.Items.GetItemsAt(site.Position, site.Z, groundOnly: true).ToList();
        if (items.Count == 0) return;

        var safe = FindSafeCellAround(site);
        if (!safe.HasValue) return;

        foreach (var it in items)
        {
            var oldPos = it.Position;
            _world.Items.UpdateItemPosition(it.Guid, oldPos, it.Z, safe.Value, it.Z);
            try
            {
                _world.Items.MergeStacksAt(safe.Value, it.Z);
            }
            catch { }
        }
    }

    private Point? FindSafeCellAround(PlaceableInstance site)
    {
        var visited = new HashSet<(int,int)>();
        var q = new Queue<(int x,int y,int d)>();
        void Enq(int x,int y,int d){ if (!_world.IsValidPosition(x,y,site.Z)) return; if (visited.Add((x,y))) q.Enqueue((x,y,d)); }
        foreach (var (dx,dy) in new (int,int)[]{ (1,0),(-1,0),(0,1),(0,-1) }) Enq(site.Position.X+dx, site.Position.Y+dy, 1);
        foreach (var (dx,dy) in new (int,int)[]{ (2,0),(-2,0),(0,2),(0,-2),(1,1),(1,-1),(-1,1),(-1,-1) }) Enq(site.Position.X+dx, site.Position.Y+dy, 2);
        while (q.Count > 0)
        {
            var (x,y,d) = q.Dequeue();
            if (d > 2) break;
            var tile = _world.GetTile(x,y,site.Z);
            if (tile == null) continue;
            if (!(tile.Value.IsStandable || tile.Value.IsWalkable)) continue;
            // Exclude construction site anchors
            int cx = x / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int cy = y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int lx = x % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int ly = y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            var ck = new HumanFortress.Simulation.World.ChunkKey(cx, cy, site.Z);
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
            return new Point(x,y);
        }
        return null;
    }

    private static bool MatchesRequirement(IEnumerable<string> itemTags, string requirement)
    {
        var set = new HashSet<string>(itemTags, StringComparer.OrdinalIgnoreCase);
        switch (requirement.ToLowerInvariant())
        {
            case "block":
                // Simplified dev tag: any item tagged 'block' or legacy aliases
                return set.Contains("block") || set.Contains("stone_block") || set.Contains("brick") || (set.Contains("stone") && set.Contains("block"));
            case "plank":
                // Simplified dev tag: any item tagged 'plank' or legacy aliases
                return set.Contains("plank") || set.Contains("wood_plank") || (set.Contains("wood") && set.Contains("plank"));
            case "stone_block":
                return set.Contains("stone") && set.Contains("block");
            case "wood_plank":
                return set.Contains("wood") && set.Contains("plank");
            case "wood_log":
                return set.Contains("wood") && set.Contains("log");
            default:
                return set.Contains(requirement);
        }
    }

    private static string FormatDict(Dictionary<string, int> d)
    {
        return "{" + string.Join(", ", d.Select(kv => $"{kv.Key}:{kv.Value}")) + "}";
    }

    private static HumanFortress.Simulation.Tiles.TerrainKind MapTargetIdToKind(string targetId, int z)
    {
        // targetId like "l0:Wall" / "l0:Floor" / "l0:Ramp" / "l0:Stairs"
        string shape = targetId;
        int idx = targetId.IndexOf(':');
        if (idx >= 0 && idx + 1 < targetId.Length) shape = targetId.Substring(idx + 1);
        if (shape.Equals("Wall", StringComparison.OrdinalIgnoreCase)) return HumanFortress.Simulation.Tiles.TerrainKind.SolidWall;
        if (shape.Equals("Floor", StringComparison.OrdinalIgnoreCase)) return HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor;
        if (shape.Equals("Ramp", StringComparison.OrdinalIgnoreCase)) return HumanFortress.Simulation.Tiles.TerrainKind.Ramp;
        if (shape.Equals("Stairs", StringComparison.OrdinalIgnoreCase)) return HumanFortress.Simulation.Tiles.TerrainKind.StairsUD;
        return HumanFortress.Simulation.Tiles.TerrainKind.OpenNoFloor;
    }

    private void EmitMoveCreature(Guid creatureId, HumanFortress.Navigation.Point3 dest)
    {
        if (_diff == null) return;
        uint eid = ToEntity(creatureId);
        int chunkX = dest.X / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int chunkY = dest.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localX = dest.X % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localY = dest.Y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int localIndex = localY * HumanFortress.Simulation.World.Chunk.SIZE_XY + localX;
        int chunkId = EncodeChunkId(new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, dest.Z));
        var target = new DiffTarget(chunkId, localIndex, unchecked((int)eid));
        _diff.AddOp(new DiffOp(DiffOpType.MoveCreature, target, SystemId, Priority));
    }

    private static uint ToEntity(Guid g)
    {
        var bytes = g.ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
    }
}
