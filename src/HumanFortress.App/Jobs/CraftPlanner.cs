using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.App.Jobs;

/// <summary>
/// Planner that inspects workshop queues, requests materials, and produces planned craft jobs.
/// </summary>
public sealed class CraftPlanner : ITick
{
    private readonly World _world;
    private readonly ITransportIntake _transport;
    private readonly WorkshopTunings _tunings;
    private readonly ConcurrentQueue<PlannedCraftJob> _outbox = new();
    private readonly int _scanBudgetPerTick;
    private const int RequestRetryTicks = 80;

    public CraftPlanner(World world, ITransportIntake transport, WorkshopTunings tunings, int scanBudgetPerTick = 24)
    {
        _world = world;
        _transport = transport;
        _tunings = tunings;
        _scanBudgetPerTick = Math.Max(1, scanBudgetPerTick);
    }

    public int Priority => UpdateOrder.Priority.Items;
    public string SystemId => "Jobs.CraftPlanner";

    public void ReadTick(ulong tick)
    {
        int scanned = 0;
        foreach (var (placeable, def) in EnumerateWorkshops())
        {
            if (scanned >= _scanBudgetPerTick) break;
            scanned++;
            var state = placeable.Workshop;
            if (state == null || state.Queue.Count == 0) continue;
            if (state.ActiveJobs >= state.AllowedWorkers) continue;

            var entry = state.Queue[0];
            var recipe = RecipeRegistry.Instance.GetRecipe(entry.RecipeId);
            if (recipe == null)
            {
                entry.Status = CraftQueueStatus.Pending;
                entry.BlockingReason = "Recipe missing";
                continue;
            }

            if (!HasMaterials(placeable, state, entry, recipe, tick))
                continue;

            if (entry.IsScheduled) continue;
            entry.IsScheduled = true;
            entry.Status = CraftQueueStatus.Scheduled;
            entry.BlockingReason = null;
            var job = new PlannedCraftJob(
                WorkshopGuid: placeable.Guid,
                QueueEntryId: entry.EntryId,
                RecipeId: recipe.Id,
                DurationTicks: recipe.DurationTicks,
                Anchor: placeable.Position,
                Z: placeable.Z);
            _outbox.Enqueue(job);
        }
    }

    public void WriteTick(ulong tick)
    {
        // Planner is read-only.
    }

    public int DequeuePlannedJobs(int max, IList<PlannedCraftJob> into)
    {
        int n = 0;
        while (n < max && _outbox.TryDequeue(out var job))
        {
            into.Add(job);
            n++;
        }
        return n;
    }

    private bool HasMaterials(PlaceableInstance placeable, WorkshopState state, CraftQueueEntry entry, RecipeDefinition recipe, ulong tick)
    {
        if (recipe.Inputs.Length == 0) return true;

        var delivered = CountInputsOnFootprint(placeable);
        bool ready = true;
        foreach (var ingredient in recipe.Inputs)
        {
            delivered.TryGetValue(ingredient.DefId, out var have);
            if (have >= ingredient.Count) continue;

            ready = false;
            entry.Status = CraftQueueStatus.AwaitingMaterials;
            entry.BlockingReason = $"Need {ingredient.Count}× {ingredient.DefId}";
            if (state.AutoRequestMaterials && (!entry.HasPendingRequests || tick - entry.LastRequestTick >= RequestRetryTicks))
            {
                if (RequestMaterials(placeable, ingredient.DefId, ingredient.Count - have, tick) > 0)
                {
                    entry.HasPendingRequests = true;
                    entry.LastRequestTick = tick;
                }
            }
            break;
        }

        if (ready)
        {
            entry.HasPendingRequests = false;
            entry.Status = CraftQueueStatus.Pending;
            entry.BlockingReason = null;
        }
        return ready;
    }

    private Dictionary<string, int> CountInputsOnFootprint(PlaceableInstance placeable)
    {
        var delivered = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var footprintCells = EnumerateFootprintAndRing(placeable).ToHashSet();
        foreach (var item in _world.Items.GetAllInstances())
        {
            if (item.IsCarried) continue;
            if (item.Z != placeable.Z) continue;
            if (!footprintCells.Contains((item.Position.X, item.Position.Y))) continue;
            delivered[item.DefinitionId] = delivered.GetValueOrDefault(item.DefinitionId, 0) + item.StackCount;
        }
        return delivered;
    }

    private int RequestMaterials(PlaceableInstance workshop, string defId, int amountNeeded, ulong tick)
    {
        int requested = 0;
        foreach (var item in _world.Items.GetAllInstances().OrderBy(i => i.Guid))
        {
            if (requested >= amountNeeded) break;
            if (item.DefinitionId != defId) continue;
            if (item.IsCarried) continue;
            if (_world.Reservations.IsItemReserved(item.Guid, tick)) continue;
            if (IsOnWorkshop(workshop, item.Position.X, item.Position.Y, item.Z)) continue;

            int take = Math.Min(amountNeeded - requested, item.StackCount);
            if (take <= 0) continue;

            var dropCell = new Point(workshop.Position.X, workshop.Position.Y);
            var req = new TransportRequest(
                ItemGuid: item.Guid,
                From: item.Position,
                FromZ: item.Z,
                To: dropCell,
                ToZ: workshop.Z,
                Quantity: take,
                Reason: TransportReason.ToWorkshopInput,
                Priority: 45,
                RequestorId: SystemId,
                CreatedTick: tick,
                Seed: SeedFrom(item.Guid));
            _transport.Enqueue(in req);
            requested += take;
        }
        return requested;
    }

    private IEnumerable<(PlaceableInstance, ConstructionDefinition?)> EnumerateWorkshops()
    {
        var registry = ConstructionRegistry.Instance;
        var list = new List<(PlaceableInstance, ConstructionDefinition?)>();
        foreach (var chunk in _world.GetAllChunks())
        {
            var pd = chunk.GetPlaceableData();
            if (pd == null) continue;
            foreach (var p in pd.GetAllOwnedPlaceables())
            {
                if (p.Workshop == null) continue;
                var def = registry.GetConstruction(p.DefinitionId);
                list.Add((p, def));
            }
        }
        return list
            .OrderBy(t => t.Item1.Z)
            .ThenBy(t => t.Item1.Position.Y)
            .ThenBy(t => t.Item1.Position.X);
    }

    private static IEnumerable<(int X, int Y)> EnumerateFootprintAndRing(PlaceableInstance placeable)
    {
        var seen = new HashSet<(int, int)>();
        var fp = placeable.Footprint;
        for (int dy = 0; dy < fp.D; dy++)
        for (int dx = 0; dx < fp.W; dx++)
        {
            int x = placeable.Position.X + dx;
            int y = placeable.Position.Y + dy;
            if (seen.Add((x, y))) yield return (x, y);
        }

        var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        for (int dy = 0; dy < fp.D; dy++)
        for (int dx = 0; dx < fp.W; dx++)
        {
            int baseX = placeable.Position.X + dx;
            int baseY = placeable.Position.Y + dy;
            foreach (var (dX, dY) in dirs)
            {
                int nx = baseX + dX;
                int ny = baseY + dY;
                if (seen.Add((nx, ny)))
                    yield return (nx, ny);
            }
        }
    }

    private static bool IsOnWorkshop(PlaceableInstance placeable, int x, int y, int z)
    {
        if (z != placeable.Z) return false;
        var fp = placeable.Footprint;
        return x >= placeable.Position.X && x < placeable.Position.X + fp.W
               && y >= placeable.Position.Y && y < placeable.Position.Y + fp.D;
    }

    private static uint SeedFrom(Guid id)
    {
        var bytes = id.ToByteArray();
        uint hash = 2166136261;
        foreach (var b in bytes)
            hash = (hash ^ b) * 16777619;
        return hash;
    }
}

public readonly record struct PlannedCraftJob(
    Guid WorkshopGuid,
    Guid QueueEntryId,
    string RecipeId,
    int DurationTicks,
    Point Anchor,
    int Z);
