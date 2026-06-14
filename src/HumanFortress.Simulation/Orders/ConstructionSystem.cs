using System;
using System.Collections.Generic;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.Placeables;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Planner for L0 structural construction. Reads ConstructionDesignation and produces PlannedBuilds.
/// - Multi-Z prism support: top layer -> StairsDown, bottom -> StairsUp, middle -> StairsUD when Shape=Stairs.
/// - Material resolution: queries ContentRegistry for (material, kind) → geology handle, with last-used preference cache.
/// - WriteTick: places L2 ghost placeables for visualization/claiming; actual L0 SetTerrain is left to executor/jobs.
/// </summary>
public sealed class ConstructionSystem : ITick
{
    private readonly World.World _world;
    private readonly OrdersManager _orders;
    private readonly IConstructionTerrainMaterialResolver _terrainMaterials;
    private readonly int _maxPerTick;

    private readonly List<PlannedBuild> _planned = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<PlannedBuild> _outbox = new();
    private readonly HumanFortress.Core.Content.Registry.ConstructionTuning _tuning;

    public ConstructionSystem(
        World.World world,
        OrdersManager orders,
        IConstructionTerrainMaterialResolver terrainMaterials,
        HumanFortress.Core.Content.Registry.ConstructionTuning tuning,
        int maxPerTick = 256)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _terrainMaterials = terrainMaterials ?? throw new ArgumentNullException(nameof(terrainMaterials));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _maxPerTick = Math.Max(1, maxPerTick);
    }

    public int Priority => UpdateOrder.Priority.Furniture; // ghost placement touches L2
    public string SystemId => "Orders.Construction";

    public void ReadTick(ulong tick)
    {
        _planned.Clear();
        var desigs = new List<ConstructionDesignation>();
        _orders.DrainConstructionDesignations(desigs, maxCount: 4);
        if (desigs.Count == 0) return;

        int count = 0;
        foreach (var d in desigs)
        {
            int addedForThisRect = 0;
            bool debugCells = (d.WorldRect.Width * d.WorldRect.Height) <= 12;
            for (int z = d.ZMin; z <= d.ZMax; z++)
            {
                var shapeKind = ResolveTargetKind(d.Shape, z, d.ZMin, d.ZMax);
                for (int y = d.WorldRect.Y; y <= d.WorldRect.MaxExtentY; y++)
                {
                    for (int x = d.WorldRect.X; x <= d.WorldRect.MaxExtentX; x++)
                    {
                        if (count >= _maxPerTick) break;
                        var tileOpt = _world.GetTile(x, y, z);
                        if (tileOpt == null)
                        {
                            if (debugCells)
                                OrdersManager.LogCallback?.Invoke($"[ORDERS.CONSTR.CELL] ({x},{y},{z}) SKIP reason=OutOfWorld");
                            continue;
                        }

                        // Basic legality checks (L0-only; L2 ghost always allowed here)
                        if (!IsBuildCandidate(tileOpt.Value, d.Shape, x, y, z))
                        {
                            if (debugCells)
                                OrdersManager.LogCallback?.Invoke($"[ORDERS.CONSTR.CELL] ({x},{y},{z}) SKIP reason=CandidateFail");
                            continue;
                        }

                        // Resolve geology handle from filter (material preference) + target kind
                        ushort geo = _terrainMaterials.ResolveGeologyHandle(d.Filter, shapeKind);
                        if (geo == 0)
                        {
                            // Fallback: attempt from current tile material (if any)
                            geo = _terrainMaterials.TryMatchFromCurrent(tileOpt.Value, shapeKind);
                            if (geo == 0)
                            {
                                if (debugCells)
                                    OrdersManager.LogCallback?.Invoke($"[ORDERS.CONSTR.CELL] ({x},{y},{z}) SKIP reason=GeoFail");
                                continue; // cannot resolve material-kind
                            }
                        }

                        _planned.Add(new PlannedBuild(new Point(x, y), z, shapeKind, geo, d.Shape, d.Filter.Tags ?? Array.Empty<string>(), d.Priority, SeedFrom(x, y, z)));
                        addedForThisRect++;
                        count++;
                        if (debugCells)
                            OrdersManager.LogCallback?.Invoke($"[ORDERS.CONSTR.CELL] ({x},{y},{z}) OK");
                    }
                    if (count >= _maxPerTick) break;
                }
                if (count >= _maxPerTick) break;
            }
            // Log summary for this rectangle
            try
            {
                OrdersManager.LogCallback?.Invoke($"[ORDERS.CONSTR] rect=({d.WorldRect.X},{d.WorldRect.Y},{d.WorldRect.Width}x{d.WorldRect.Height}) z={d.ZMin}..{d.ZMax} shape={d.Shape} planned={addedForThisRect}");
            }
            catch { }
            if (count >= _maxPerTick) break;
        }
    }

    public void WriteTick(ulong tick)
    {
        if (_planned.Count == 0) return;

        // Create construction sites instead of ghosts; do not enqueue PlannedBuild for immediate L0 changes.
        foreach (var p in _planned)
        {
            try
            {
                var tuning = _tuning; // local
                var req = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                // Map shape + filter tags to required item tags
                // We rely on designation filter tags: e.g., ["stone_block"], ["wood_log"], ["wood_plank"], or combination for Ramp
                // When tags are missing, default to stone_block for Wall/Floor and both for Ramp
                // Preferred from RequiredTags if provided
                var tags = p.RequiredTags ?? Array.Empty<string>();
                if (tags.Length > 0)
                {
                    foreach (var tag in tags)
                    {
                        switch (tag.ToLowerInvariant())
                        {
                            case "stone_block": req["stone_block"] = req.GetValueOrDefault("stone_block") + 1; break;
                            case "wood_plank": req["wood_plank"] = req.GetValueOrDefault("wood_plank") + 1; break;
                            case "wood_log": req["wood_log"] = req.GetValueOrDefault("wood_log") + 1; break;
                        }
                    }
                    // Scale counts by shape defaults
                    if (p.Shape == ConstructionShape.Wall)
                    {
                        if (req.ContainsKey("stone_block")) req["stone_block"] = _tuning.WallBlockCount;
                        if (req.ContainsKey("wood_log")) req["wood_log"] = _tuning.WallBlockCount; // reuse count
                    }
                    else if (p.Shape == ConstructionShape.Floor)
                    {
                        if (req.ContainsKey("stone_block")) req["stone_block"] = _tuning.FloorBlockCount;
                        if (req.ContainsKey("wood_plank")) req["wood_plank"] = _tuning.FloorPlankCount;
                    }
                    else if (p.Shape == ConstructionShape.Ramp)
                    {
                        // Ramp requires both; if only one selected, complement the other
                        req["stone_block"] = _tuning.RampBlockCount;
                        req["wood_plank"] = _tuning.RampPlankCount;
                    }
                }
                else
                {
                    // Defaults per shape
                    switch (p.Shape)
                    {
                        case ConstructionShape.Wall:
                            req["stone_block"] = tuning.WallBlockCount;
                            break;
                        case ConstructionShape.Floor:
                            req["stone_block"] = tuning.FloorBlockCount;
                            break;
                        case ConstructionShape.Ramp:
                            req["stone_block"] = tuning.RampBlockCount;
                            req["wood_plank"] = tuning.RampPlankCount;
                            break;
                        case ConstructionShape.Stairs:
                            req["stone_block"] = tuning.StairBlockCount;
                            break;
                    }
                }

                // Create site with 1x1 footprint
                var site = PlaceableFactory.CreateConstructionSite(
                    p.Cell,
                    p.Z,
                    tick,
                    targetId: $"l0:{p.Shape}",
                    fp: new HumanFortress.Core.Content.Registry.Footprint(1,1,1),
                    materialsRequired: req,
                    totalBuildTicks: p.Shape switch {
                        ConstructionShape.Wall => tuning.BuildTicksWall,
                        ConstructionShape.Floor => tuning.BuildTicksFloor,
                        ConstructionShape.Ramp => tuning.BuildTicksRamp,
                        ConstructionShape.Stairs => tuning.BuildTicksStairs,
                        _ => tuning.BuildTicksFloor }
                );
                PlaceableManager.PlacePlaceable(_world, site, tick);
            }
            catch (Exception)
            {
                // swallow per-site errors to avoid breaking the loop
            }
        }
        _planned.Clear();
    }

    public int DequeuePlannedBuilds(int max, IList<PlannedBuild> into)
    {
        int n = 0;
        while (n < max && _outbox.TryDequeue(out var m))
        {
            into.Add(m);
            n++;
        }
        return n;
    }

    /// <summary>
    /// Helper to pack SetTerrain args with optional geology override (plan A: existing opcode).
    /// </summary>
    public static ulong PackSetTerrainArgs(HumanFortress.Simulation.Tiles.TerrainKind kind, ushort geologyHandle)
    {
        return ((ulong)kind & 0xFFUL) | (((ulong)geologyHandle & 0xFFFFUL) << 8);
    }

    private static HumanFortress.Simulation.Tiles.TerrainKind ResolveTargetKind(ConstructionShape shape, int z, int zMin, int zMax)
    {
        return shape switch
        {
            ConstructionShape.Wall => HumanFortress.Simulation.Tiles.TerrainKind.SolidWall,
            ConstructionShape.Floor => HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor,
            ConstructionShape.Ramp => HumanFortress.Simulation.Tiles.TerrainKind.Ramp,
            ConstructionShape.Stairs => (z == zMin && z == zMax)
                ? HumanFortress.Simulation.Tiles.TerrainKind.StairsUD
                : (z == zMin ? HumanFortress.Simulation.Tiles.TerrainKind.StairsUp : (z == zMax ? HumanFortress.Simulation.Tiles.TerrainKind.StairsDown : HumanFortress.Simulation.Tiles.TerrainKind.StairsUD)),
            _ => HumanFortress.Simulation.Tiles.TerrainKind.OpenNoFloor
        };
    }

    private bool IsBuildCandidate(HumanFortress.Simulation.Tiles.TileBase tile, ConstructionShape shape, int x, int y, int z)
    {
        // Minimal fast checks + a few topology gates for floor/ramp.
        if (shape == ConstructionShape.Floor)
        {
            if (tile.Kind == HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor) return false; // duplicate
            if (_tuning.FloorRequiresSupport)
            {
                var below = _world.GetTile(x, y, z - 1);
                bool support = below != null && below.Value.ProvidesSupport;
                if (!support && _tuning.FloorAllowNeighborSupport)
                {
                    // NESW neighbor support below
                    static IEnumerable<(int dx,int dy)> Adj() { yield return (0,-1); yield return (1,0); yield return (0,1); yield return (-1,0); }
                    foreach (var (dx, dy) in Adj())
                    {
                        var b2 = _world.GetTile(x + dx, y + dy, z - 1);
                        if (b2 != null && b2.Value.ProvidesSupport) { support = true; break; }
                    }
                }
                if (!support) return false;
            }
            return true;
        }

        if (shape == ConstructionShape.Wall)
        {
            if (tile.Kind == HumanFortress.Simulation.Tiles.TerrainKind.SolidWall) return false;
            // Optional: require some support below as policy; leave to executor for now.
            return true;
        }

        if (shape == ConstructionShape.Ramp)
        {
            if (tile.Kind == HumanFortress.Simulation.Tiles.TerrainKind.Ramp) return false;
            // DF ramp gates: (x,y,z+1) must be OpenNoFloor; at least one neighbor at z+1 is standable
            var top = _world.GetTile(x, y, z + 1);
            if (top == null || top.Value.Kind != HumanFortress.Simulation.Tiles.TerrainKind.OpenNoFloor) return false;
            bool hasStand = false;
            for (int dy = -1; dy <= 1 && !hasStand; dy++)
                for (int dx = -1; dx <= 1 && !hasStand; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var t = _world.GetTile(x + dx, y + dy, z + 1);
                    if (t != null && t.Value.IsStandable) hasStand = true;
                }
            return hasStand;
        }

        if (shape == ConstructionShape.Stairs)
        {
            return tile.Kind is not HumanFortress.Simulation.Tiles.TerrainKind.StairsUp and not HumanFortress.Simulation.Tiles.TerrainKind.StairsDown and not HumanFortress.Simulation.Tiles.TerrainKind.StairsUD;
        }

        return false;
    }

    private static ulong SeedFrom(int x, int y, int z)
    {
        unchecked
        {
            uint s = 2166136261;
            s = (s ^ (uint)x) * 16777619;
            s = (s ^ (uint)y) * 16777619;
            s = (s ^ (uint)z) * 16777619;
            return s;
        }
    }
}
