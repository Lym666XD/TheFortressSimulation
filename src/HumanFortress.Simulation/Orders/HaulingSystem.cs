using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Time;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Minimal v1 hauling system: reads haul designations, plans moves, and applies instant relocations.
/// Design for extensibility: later replace instant moves with creature-assigned jobs and path execution.
/// </summary>
public sealed class HaulingSystem : ITick
{
    private readonly World.World _world;
    private readonly OrdersManager _orders;
    private readonly int _maxPerTick;

    // Planned relocations from Read phase; exposed to job system.
    private readonly List<PlannedMove> _planned = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<PlannedMove> _outbox = new();

    public HaulingSystem(World.World world, OrdersManager orders, int maxPerTick = 128)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _maxPerTick = Math.Max(1, maxPerTick);
    }

    public int Priority => UpdateOrder.Priority.Items; // Ensure writes align with Items stage
    public string SystemId => "Jobs.Hauling";

    public void ReadTick(ulong tick)
    {
        _planned.Clear();

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
            var items = _world.Items.GetAllInstances()
                .Where(i => i.Z == d.Z && d.WorldRect.Contains(i.Position) && !i.IsCarried && !IsInStockpile(i))
                .ToList();

            foreach (var item in items)
            {
                if (plannedCount >= _maxPerTick) break;
                // Skip if centrally reserved (TTL based)
                if (_world.Reservations.IsItemReserved(item.Guid, tick)) continue;
                if (item.IsReserved) continue;

                // Choose nearest accepting zone cell (v1: first shard member cell)
                if (!TryFindDestination(item, zones, out var destWorld, out var toZ))
                    continue;


                _planned.Add(new PlannedMove
                {
                    ItemGuid = item.Guid,
                    From = item.Position,
                    FromZ = item.Z,
                    To = destWorld,
                    ToZ = toZ
                });
                plannedCount++;
            }
        }
    }

    private bool IsInStockpile(Items.ItemInstance item)
    {
        int worldX = item.Position.X;
        int worldY = item.Position.Y;
        int z = item.Z;
        int cx = worldX / Chunk.SIZE_XY;
        int cy = worldY / Chunk.SIZE_XY;
        int lx = worldX % Chunk.SIZE_XY;
        int ly = worldY % Chunk.SIZE_XY;
        var ck = new ChunkKey(cx, cy, z);
        var chunk = _world.GetChunk(ck);
        if (chunk == null) return false;
        var stock = chunk.GetStockpileData();
        if (stock == null) return false;
        int cell = Chunk.LocalIndex(lx, ly);
        return stock.GetZoneAtCell(cell) > 0;
    }

    public void WriteTick(ulong tick)
    {
        if (_planned.Count == 0) return;
        // Hand off to job system via outbox (real execution happens elsewhere)
        foreach (var move in _planned)
            _outbox.Enqueue(move);
        _planned.Clear();
    }

    private List<StockpileZone> GetAllStockpileZones()
    {
        // Authority source: World.Stockpiles
        return _world.Stockpiles.GetAllZones().ToList();
    }

    private bool TryFindDestination(Items.ItemInstance item, List<StockpileZone> zones, out Point destWorld, out int destZ)
    {
        // v1: pick the member cell in the nearest zone by Manhattan distance (chunk-level, includes Z) and use the first cell in that shard
        destWorld = default;
        destZ = item.Z;

        var itemChunkX = item.Position.X / Chunk.SIZE_XY;
        var itemChunkY = item.Position.Y / Chunk.SIZE_XY;
        var itemChunkKey = new ChunkKey(itemChunkX, itemChunkY, item.Z);

        StockpileZone? bestZone = null;
        int bestDist = int.MaxValue;

        foreach (var zone in zones)
        {
            // Must contain at least one shard at this Z
            foreach (var ck in zone.MemberChunks)
            {
                int dist = Math.Abs(ck.ChunkX - itemChunkKey.ChunkX) + Math.Abs(ck.ChunkY - itemChunkKey.ChunkY) + Math.Abs(ck.Z - itemChunkKey.Z);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestZone = zone;
                }
            }
        }

        if (bestZone == null) return false;

        // Choose first member cell from the nearest shard
        foreach (var ck in bestZone.MemberChunks)
        {
            var chunk = _world.GetChunk(ck);
            if (chunk == null) continue;
            var stock = chunk.GetStockpileData();
            if (stock == null) continue;
            var shard = stock.GetShard(bestZone.ZoneId);
            if (shard == null) continue;

            // Pick the first cell belonging to the shard
            for (int idx = 0; idx < shard.MemberCells.Length; idx++)
            {
                if (!shard.MemberCells[idx]) continue;
                var (lx, ly) = Chunk.IndexToLocal(idx);
                destWorld = new Point(ck.ChunkX * Chunk.SIZE_XY + lx, ck.ChunkY * Chunk.SIZE_XY + ly);
                destZ = ck.Z;
                return true;
            }
        }

        return false;
    }

    public struct PlannedMove
    {
        public Guid ItemGuid;
        public Point From;
        public int FromZ;
        public Point To;
        public int ToZ;
    }

    /// <summary>
    /// Dequeue up to max planned moves for job creation.
    /// </summary>
    public int DequeuePlannedMoves(int max, IList<PlannedMove> into)
    {
        int n = 0;
        while (n < max && _outbox.TryDequeue(out var m))
        {
            into.Add(m);
            n++;
        }
        return n;
    }
}
