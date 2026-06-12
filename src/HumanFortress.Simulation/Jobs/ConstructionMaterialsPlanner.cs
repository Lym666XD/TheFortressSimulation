using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.Diagnostics;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Jobs
{
    /// <summary>
    /// Planner that scans construction sites and enqueues material transport requests.
    /// v1.1: naive item selection by tag and nearest distance; no advanced filters.
    /// Integrates with the unified Jobs stage (Read-only).
    /// </summary>
    public sealed class ConstructionMaterialsPlanner : ITick
    {
        public static Action<string>? LogCallback { get; set; }
        private readonly World.World _world;
        private readonly ITransportIntake _intake;
        private readonly IItemDefinitionCatalog _itemDefinitions;
        private readonly int _scanBudgetPerTick;

        public ConstructionMaterialsPlanner(
            World.World world,
            ITransportIntake intake,
            IItemDefinitionCatalog itemDefinitions,
            int scanBudgetPerTick = 64)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _intake = intake ?? throw new ArgumentNullException(nameof(intake));
            _itemDefinitions = itemDefinitions ?? throw new ArgumentNullException(nameof(itemDefinitions));
            _scanBudgetPerTick = Math.Max(1, scanBudgetPerTick);
        }

        public int Priority => HumanFortress.Core.Simulation.UpdateOrder.Priority.Items; // Read-only planner in Jobs stage
        public string SystemId => "Jobs.ConstructionMaterialsPlanner";

        public void ReadTick(ulong tick)
        {
            int scannedSites = 0;
            int enqueued = 0;

            foreach (var chunk in _world.GetAllChunks())
            {
                var pd = chunk.GetPlaceableData();
                if (pd == null) continue;
                foreach (var p in pd.GetAllOwnedPlaceables())
                {
                    if (scannedSites >= _scanBudgetPerTick) break;

                    // Treat any placeable with ConstructionSite state as a site
                    if (p.ConstructionSite == null) continue;
                    scannedSites++;

                    var site = p.ConstructionSite;
                    // Derive delivered counts by scanning footprint cells for matching items (simple O(N) pass)
                    var delivered = CountDeliveredOnFootprintOrRing(p);

                    foreach (var kv in site.MaterialsRequired)
                    {
                        string tag = kv.Key;
                        int need = kv.Value - delivered.GetValueOrDefault(tag, 0);
                        if (need <= 0) continue;

                        Log($"[CM-PLAN][{tick}] shortfall site=({p.Position.X},{p.Position.Y},{p.Z}) req={tag} need={need} delivered={delivered.GetValueOrDefault(tag,0)}");

                        // Enqueue as few requests as possible with explicit Quantity (bounded by per-tick budget)
                        while (need > 0 && enqueued < _scanBudgetPerTick)
                        {
                            var itemGuid = TryFindNearestItemByTag(tag, p.Position.X, p.Position.Y, p.Z, tick);
                            if (itemGuid == null)
                            {
                                Log($"[CM-PLAN][{tick}] no-source site=({p.Position.X},{p.Position.Y},{p.Z}) req={tag} need={need}");
                                break;
                            }
                            var inst = _world.Items.GetInstance(itemGuid.Value);
                            if (inst == null)
                            {
                                Log($"[CM-PLAN][{tick}] no-inst guid={itemGuid} req={tag}");
                                break;
                            }

                            int take = System.Math.Min(need, inst.StackCount);
                            if (take <= 0)
                            {
                                Log($"[CM-PLAN][{tick}] zero-take guid={inst.Guid} stack={inst.StackCount} need={need} req={tag}");
                                break;
                            }

                            uint seed = SeedFrom(itemGuid.Value);
                            var drop = ChooseDropCellForSite(p);
                            if (drop == null)
                            {
                                Log($"[CM-PLAN][{tick}] no-drop site=({p.Position.X},{p.Position.Y},{p.Z}) req={tag}");
                                break;
                            }
                            var req = new TransportRequest(
                                ItemGuid: itemGuid.Value,
                                From: inst.Position,
                                FromZ: inst.Z,
                                To: drop.Value,
                                ToZ: p.Z,
                                Quantity: take,
                                Reason: TransportReason.ToConstructionSite,
                                Priority: 40,
                                RequestorId: SystemId,
                                CreatedTick: tick,
                                Seed: seed);
                            _intake.Enqueue(in req);
                            Log($"[CM-PLAN][{tick}] enqueue req={tag} qty={take} from=({inst.Position.X},{inst.Position.Y},{inst.Z}) to=({drop.Value.X},{drop.Value.Y},{p.Z}) item={inst.DefinitionId}");
                            enqueued++;
                            need -= take;
                        }
                    }
                }
                if (scannedSites >= _scanBudgetPerTick) break;
            }

            if (scannedSites > 0 || (tick % 300UL) == 0UL)
            {
                var msg = $"[CM-PLAN][{tick}] scanned_sites={scannedSites} enqueued={enqueued}";
                Log(msg);
            }
        }

        public void WriteTick(ulong tick)
        {
            // No writes in planner
        }

        private static uint SeedFrom(Guid a)
        {
            unchecked
            {
                var ba = a.ToByteArray();
                uint s = 2166136261;
                foreach (var t in ba) s = (s ^ t) * 16777619;
                return s;
            }
        }

        private Dictionary<string, int> CountDeliveredOnFootprintOrRing(PlaceableInstance site)
        {
            var delivered = new Dictionary<string, int>();
            var fp = site.Footprint;
            foreach (var cell in EnumerateFootprintAndRing(site))
            {
                foreach (var it in _world.Items.GetGroundItemsAt(cell, site.Z))
                {
                    if (it.Position.X == cell.X && it.Position.Y == cell.Y && it.Z == site.Z)
                    {
                        var def = _itemDefinitions.GetDefinition(it.DefinitionId);
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
            }
            return delivered;
        }

        private Guid? TryFindNearestItemByTag(string reqTag, int toX, int toY, int toZ, ulong tick)
        {
            Guid? best = null;
            int bestDist = int.MaxValue;
            var groundItems = _world.Items.GetGroundInstances().ToList();

            // Diagnostic: log total item count every 100 ticks
            if (tick % 100 == 0)
            {
                Log($"[CM-DIAG][{tick}] TryFindNearestItemByTag: reqTag={reqTag}, groundItems={groundItems.Count}");
            }

            int skippedReserved = 0, skippedNoDef = 0, skippedNoMatch = 0, candidates = 0;

            foreach (var it in groundItems)
            {
                if (_world.Reservations.IsItemReserved(it.Guid, tick)) { skippedReserved++; continue; }
                var def = _itemDefinitions.GetDefinition(it.DefinitionId);
                if (def == null || def.Tags == null) { skippedNoDef++; continue; }
                if (!MatchesRequirement(def.Tags, reqTag)) { skippedNoMatch++; continue; }
                candidates++;
                int d = Math.Abs(it.Position.X - toX) + Math.Abs(it.Position.Y - toY) + Math.Abs(it.Z - toZ);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = it.Guid;
                }
            }

            // Log diagnostic summary when no source found
            if (best == null)
            {
                Log($"[CM-DIAG][{tick}] NO SOURCE for {reqTag}: ground={groundItems.Count} reserved={skippedReserved} noDef={skippedNoDef} noMatch={skippedNoMatch} candidates={candidates}");
            }

            return best;
        }

        private static bool MatchesRequirement(IEnumerable<string> itemTags, string requirement)
        {
            var set = new HashSet<string>(itemTags, StringComparer.OrdinalIgnoreCase);
            switch (requirement.ToLowerInvariant())
            {
                case "block":
                    return set.Contains("block") || set.Contains("stone_block") || set.Contains("brick") || (set.Contains("stone") && set.Contains("block"));
                case "plank":
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

        private IEnumerable<Point> EnumerateFootprintAndRing(PlaceableInstance site)
        {
            var seen = new HashSet<(int,int)>();
            var fp = site.Footprint;
            // footprint
            for (int dy = 0; dy < fp.D; dy++)
            for (int dx = 0; dx < fp.W; dx++)
            {
                int wx = site.Position.X + dx;
                int wy = site.Position.Y + dy;
                if (seen.Add((wx, wy))) yield return new Point(wx, wy);
            }
            // ring (4-neighbor per footprint cell)
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

        private Point? ChooseDropCellForSite(PlaceableInstance site)
        {
            // Search ring1 -> ring2 -> ring3 for a safe drop cell that is not a construction site anchor
            foreach (var radius in new int[] { 1, 2, 3 })
            {
                foreach (var cell in EnumerateRing(site.Position, site.Z, radius))
                {
                    var tile = _world.GetTile(cell.X, cell.Y, site.Z);
                    if (tile == null) continue;
                    if (!(tile.Value.IsStandable || tile.Value.IsWalkable)) continue;
                    if (IsConstructionSiteAnchor(cell.X, cell.Y, site.Z)) continue;
                    return cell;
                }
            }
            return null;
        }

        private IEnumerable<Point> EnumerateRing(Point center, int z, int radius)
        {
            if (radius <= 0) yield break;
            if (radius == 1)
            {
                var dirs = new (int dx,int dy)[]{ (1,0),(-1,0),(0,1),(0,-1) };
                foreach (var (dx,dy) in dirs)
                {
                    int x = center.X + dx; int y = center.Y + dy;
                    if (_world.IsValidPosition(x,y,z)) yield return new Point(x,y);
                }
                yield break;
            }
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int x = center.X + dx; int y = center.Y + dy;
                    if (!_world.IsValidPosition(x,y,z)) continue;
                    int manhattan = System.Math.Abs(dx) + System.Math.Abs(dy);
                    if (manhattan < radius) continue; // prefer boundary of this ring
                    yield return new Point(x,y);
                }
            }
        }

        private bool IsConstructionSiteAnchor(int x, int y, int z)
        {
            int cx = x / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int cy = y / HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int lx = x % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            int ly = y % HumanFortress.Simulation.World.Chunk.SIZE_XY;
            var ck = new HumanFortress.Simulation.World.ChunkKey(cx, cy, z);
            var chunk = _world.GetChunk(ck);
            if (chunk == null) return false;
            var pd = chunk.GetPlaceableData();
            if (pd == null) return false;
            if (pd.TryGetOwnedAt(HumanFortress.Simulation.World.Chunk.LocalIndex(lx,ly), out var owned))
            {
                return owned.ConstructionSite != null;
            }
            return false;
        }

        private static void Log(string message)
        {
            SimulationDiagnostics.Information(LogCallback, "Jobs.ConstructionMaterials", message);
        }
    }
}
