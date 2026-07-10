using System;
using System.Collections.Generic;
using HumanFortress.Contracts.Content.Registry;
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
internal sealed class BuildableConstructionSystem : ITick
{
    private readonly World.World _world;
    private readonly OrdersManager _orders;
    private readonly IConstructionCatalog _constructions;
    private readonly int _maxPerTick;
    private readonly Queue<PlaceableInstance> _outbox = new();

    internal BuildableConstructionSystem(
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

    internal int Priority => UpdateOrder.Priority.Furniture; // touches L2 layer
    internal string SystemId => "Orders.BuildableConstruction";

    int ITick.Priority => Priority;

    string ITick.SystemId => SystemId;

    internal void ReadTick(ulong tick)
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

                var req = BuildMaterialRequirements(def);

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

    void ITick.ReadTick(ulong tick)
    {
        ReadTick(tick);
    }

    internal void WriteTick(ulong tick)
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

    void ITick.WriteTick(ulong tick)
    {
        WriteTick(tick);
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

    internal static Dictionary<string, int> BuildMaterialRequirements(ConstructionDefinition definition)
    {
        var requirements = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cost in definition.MaterialCosts)
        {
            var requirementId = ToRequirementId(cost);
            if (string.IsNullOrWhiteSpace(requirementId))
                continue;

            requirements[requirementId] = requirements.GetValueOrDefault(requirementId) + Math.Max(1, cost.Count);
        }

        return requirements;
    }

    private static string ToRequirementId(MaterialCost cost)
    {
        if (!string.IsNullOrWhiteSpace(cost.Tag))
            return ConstructionMaterialRequirement.ForTag(cost.Tag!);

        return !string.IsNullOrWhiteSpace(cost.DefId)
            ? ConstructionMaterialRequirement.ForDefinition(cost.DefId!)
            : string.Empty;
    }
}
