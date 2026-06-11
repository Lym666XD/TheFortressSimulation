using System;
using System.Collections.Generic;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Planner for L2 buildable constructions (e.g., workshops).
/// Drains BuildableConstructionDesignation and places construction sites at anchors.
/// </summary>
public sealed class BuildableConstructionSystem : ITick
{
    private readonly World.World _world;
    private readonly OrdersManager _orders;
    private readonly IConstructionCatalog _constructions;
    private readonly int _maxPerTick;
    private readonly System.Collections.Concurrent.ConcurrentQueue<PlaceableInstance> _outbox = new();

    public BuildableConstructionSystem(
        World.World world,
        OrdersManager orders,
        IConstructionCatalog constructions,
        int maxPerTick = 16)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
        _maxPerTick = Math.Max(1, maxPerTick);
    }

    public int Priority => UpdateOrder.Priority.Furniture; // touches L2 layer
    public string SystemId => "Orders.BuildableConstruction";

    public void ReadTick(ulong tick)
    {
        var desigs = new List<BuildableConstructionDesignation>();
        _orders.DrainBuildableConstructions(desigs, _maxPerTick);
        if (desigs.Count == 0) return;

        foreach (var d in desigs)
        {
            try
            {
                var def = _constructions.GetConstruction(d.ConstructionId);
                if (def == null)
                {
                    OrdersManager.LogCallback?.Invoke($"[ORDERS.BUILDABLE] id={d.ConstructionId} anchor=({d.Anchor.X},{d.Anchor.Y},{d.Z}) SKIP reason=UnknownDef");
                    continue;
                }

                var fp = def.PlaceableProfile.Footprint;
                // Basic legality checks
                if (!ValidateFootprint(d.Anchor, d.Z, fp, def.PlaceableProfile.RequiresFloor))
                {
                    OrdersManager.LogCallback?.Invoke($"[ORDERS.BUILDABLE] id={d.ConstructionId} anchor=({d.Anchor.X},{d.Anchor.Y},{d.Z}) SKIP reason=IllegalFootprint");
                    continue;
                }

                // Build materials map (tag -> count). MVP: Tag only
                var req = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var mc in def.MaterialCosts)
                {
                    if (!string.IsNullOrWhiteSpace(mc.Tag))
                    {
                        req[mc.Tag!] = req.GetValueOrDefault(mc.Tag!, 0) + Math.Max(1, mc.Count);
                    }
                    else if (!string.IsNullOrWhiteSpace(mc.DefId))
                    {
                        // TODO: defId support in planner (map to item defs)
                        // For MVP, ignore defId-specific entries
                        continue;
                    }
                }

                // Create site (defer placement to WriteTick)
                var site = PlaceableFactory.CreateConstructionSite(
                    d.Anchor,
                    d.Z,
                    tick,
                    targetId: def.Id,
                    fp: fp,
                    materialsRequired: req,
                    totalBuildTicks: Math.Max(1, def.BuildTimeTicks));

                _outbox.Enqueue(site);

                OrdersManager.LogCallback?.Invoke($"[ORDERS.BUILDABLE] id={def.Id} anchor=({d.Anchor.X},{d.Anchor.Y},{d.Z}) footprint={fp.W}x{fp.D} planned=1");
            }
            catch (Exception ex)
            {
                OrdersManager.LogCallback?.Invoke($"[ORDERS.BUILDABLE] ERROR: {ex.Message}");
            }
        }
    }

    public void WriteTick(ulong tick)
    {
        int placed = 0;
        while (placed < _maxPerTick && _outbox.TryDequeue(out var site))
        {
            try
            {
                PlaceableManager.PlacePlaceable(_world, site, tick);
                placed++;
            }
            catch (Exception ex)
            {
                OrdersManager.LogCallback?.Invoke($"[ORDERS.BUILDABLE] place ERR: {ex.Message}");
            }
        }
    }

    private bool ValidateFootprint(Point anchor, int z, Footprint fp, bool requiresFloor)
    {
        for (int dy = 0; dy < fp.D; dy++)
        {
            for (int dx = 0; dx < fp.W; dx++)
            {
                int x = anchor.X + dx;
                int y = anchor.Y + dy;
                var t = _world.GetTile(x, y, z);
                if (t == null) return false;
                // Out-of-world or blocked
                if (!t.Value.IsWalkable) return false;
                if (requiresFloor)
                {
                    // Heuristic: require standable or explicit floor (engine uses IsStandable for floor tiles)
                    if (!(t.Value.IsStandable || t.Value.Kind == HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor))
                        return false;
                }
            }
        }
        // Basic collision check with existing placeables
        var coll = PlaceableManager.CheckCollision(_world, anchor, z, fp);
        return coll.CanPlace;
    }
}
