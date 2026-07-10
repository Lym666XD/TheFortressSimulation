using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Time;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Diagnostics;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Reads haul designations and turns them into transport requests.
/// </summary>
internal sealed class HaulingSystem : ITick
{
    private readonly World.World _world;
    private readonly OrdersManager _orders;
    private readonly int _maxPerTick;

    private readonly List<PlannedTransportRequest> _plannedRequests = new();
    private readonly HashSet<Guid> _plannedItems = new();
    private readonly Dictionary<(ChunkKey ChunkKey, int ZoneId), int> _plannedReservations = new();
    private readonly HumanFortress.Simulation.Jobs.ITransportIntake _transportIntake;
    private readonly StockpileDiffLog? _stockpileDiffLog;

    internal HaulingSystem(World.World world, OrdersManager orders, int maxPerTick = 128,
        HumanFortress.Simulation.Jobs.ITransportIntake? transportIntake = null,
        StockpileDiffLog? stockpileDiffLog = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _maxPerTick = Math.Max(1, maxPerTick);
        _transportIntake = transportIntake ?? throw new ArgumentNullException(nameof(transportIntake), "Transport intake is required after haul refactor");
        _stockpileDiffLog = stockpileDiffLog;
    }

    internal int Priority => UpdateOrder.Priority.Items; // Ensure writes align with Items stage
    internal string SystemId => "Jobs.Hauling";

    int ITick.Priority => Priority;

    string ITick.SystemId => SystemId;

    internal void ReadTick(ulong tick)
    {
        _plannedRequests.Clear();
        _plannedItems.Clear();
        _plannedReservations.Clear();

        // Drain a bounded number of new haul designations (one-shot mode: no persistent active set)
        var desigs = new List<HaulDesignation>();
        _orders.DrainHaulDesignations(desigs, maxCount: 8); // small budget per tick
        if (desigs.Count == 0) return;

        // Build a list of candidate zones (accept-all or with filters)
        var zones = GetAllStockpileZones();
        if (zones.Count == 0) return;

        int plannedCount = 0;

        foreach (var d in desigs)
        {
            // Enumerate items in world rectangle at Z that are not reserved/carried and not already in a stockpile cell
            var items = _world.Items.GetGroundItemsIn(d.WorldRect, d.Z)
                .Where(i => !IsInStockpile(i))
                .ToList();

            foreach (var item in items)
            {
                if (plannedCount >= _maxPerTick) break;
                if (!_plannedItems.Add(item.Guid)) continue;
                // Skip if centrally reserved (TTL based)
                if (_world.Reservations.IsItemReserved(item.Guid, tick))
                {
                    _plannedItems.Remove(item.Guid);
                    continue;
                }

                // Choose nearest accepting zone cell (v1: first shard member cell)
                if (!TryFindDestination(item, zones, out var destWorld, out var toZ))
                {
                    _plannedItems.Remove(item.Guid);
                    continue;
                }

                if (!StockpileWorldQueries.TryGetStockpileCell(_world, destWorld.X, destWorld.Y, toZ, out var destinationCell))
                {
                    _plannedItems.Remove(item.Guid);
                    continue;
                }

                _plannedRequests.Add(new PlannedTransportRequest
                {
                    ItemGuid = item.Guid,
                    From = item.Position,
                    FromZ = item.Z,
                    To = destWorld,
                    ToZ = toZ,
                    DestinationChunk = destinationCell.ChunkKey,
                    DestinationZoneId = destinationCell.ZoneId
                });
                AddPlannedReservation(destinationCell);
                plannedCount++;
            }
        }
    }

    private bool IsInStockpile(Items.ItemInstance item)
    {
        bool inZone = StockpileWorldQueries.IsItemInStockpile(_world, item);
        if (inZone)
        {
            Log($"[HAUL-PLAN] Skip item={item.Guid} already in stockpile zone at ({item.Position.X},{item.Position.Y},{item.Z})");
        }
        return inZone;
    }

    void ITick.ReadTick(ulong tick)
    {
        ReadTick(tick);
    }

    internal void WriteTick(ulong tick)
    {
        if (_plannedRequests.Count == 0) return;

        foreach (var request in _plannedRequests)
        {
            uint seed = SeedFrom(request.ItemGuid);
            var inst = _world.Items.GetInstance(request.ItemGuid);
            int qty = inst?.StackCount ?? 1;
            var req = new HumanFortress.Simulation.Jobs.TransportRequest(
                request.ItemGuid,
                request.From,
                request.FromZ,
                request.To,
                request.ToZ,
                qty,
                HumanFortress.Simulation.Jobs.TransportReason.ToStockpile,
                Priority: 60,
                RequestorId: SystemId,
                CreatedTick: tick,
                Seed: seed);
            if (_transportIntake.Enqueue(in req))
            {
                _stockpileDiffLog?.AddReserveSlot(
                    request.DestinationChunk,
                    request.DestinationZoneId,
                    Priority,
                    SystemId);
                Log($"[HAUL-PLAN][{tick}] Enqueue item={request.ItemGuid} from=({request.From.X},{request.From.Y},{request.FromZ}) to=({request.To.X},{request.To.Y},{request.ToZ}) qty={qty}");
            }
            else
            {
                Log($"[HAUL-PLAN][{tick}] Skip duplicate pending transport item={request.ItemGuid} to=({request.To.X},{request.To.Y},{request.ToZ})");
            }
        }
        _plannedRequests.Clear();
        _plannedItems.Clear();
        _plannedReservations.Clear();
    }

    void ITick.WriteTick(ulong tick)
    {
        WriteTick(tick);
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

    private List<StockpileZone> GetAllStockpileZones()
    {
        // Authority source: World.Stockpiles
        return _world.Stockpiles.GetAllZones().ToList();
    }

    private bool TryFindDestination(Items.ItemInstance item, List<StockpileZone> zones, out Point destWorld, out int destZ)
    {
        if (!StockpileWorldQueries.TryFindDestination(_world, item, zones, out destWorld, out destZ, _plannedReservations))
            return false;

        Log($"[HAUL-PLAN] Dest cell=({destWorld.X},{destWorld.Y},{destZ})");
        return true;
    }

    private void AddPlannedReservation(StockpileCellLocation location)
    {
        var key = (location.ChunkKey, location.ZoneId);
        _plannedReservations[key] = _plannedReservations.TryGetValue(key, out int current)
            ? current + 1
            : 1;
    }

    private static void Log(string message)
    {
        SimulationDiagnostics.Information(OrdersManager.LogCallback, "Jobs.Hauling", message);
    }

    internal struct PlannedTransportRequest
    {
        internal Guid ItemGuid;
        internal Point From;
        internal int FromZ;
        internal Point To;
        internal int ToZ;
        internal ChunkKey DestinationChunk;
        internal int DestinationZoneId;
    }

}
