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

        // Drain a bounded number of new haul designations
        var desigs = new List<HaulDesignation>();
        _orders.DrainHaulDesignations(desigs, maxCount: 8); // small budget per tick
        // Include persistent active designations as well so planning continues across ticks
        var active = _orders.GetActiveHaulsSnapshot();
        foreach (var a in active)
            if (!desigs.Contains(a)) desigs.Add(a);
        if (desigs.Count == 0) return;

        // Build a list of candidate zones (accept-all or with filters)
        var zones = GetAllStockpileZones();
        if (zones.Count == 0) return;

        int plannedCount = 0;

        foreach (var d in desigs)
        {
            // Enumerate items in world rectangle at Z
            var items = _world.Items.GetAllInstances()
                .Where(i => i.Z == d.Z && d.WorldRect.Contains(i.Position) && !i.IsReserved && !i.IsCarried)
                .ToList();

            foreach (var item in items)
            {
                if (plannedCount >= _maxPerTick) break;

                // Choose nearest accepting zone cell (v1: first shard member cell)
                if (!TryFindDestination(item, zones, out var destWorld))
                    continue;

                _planned.Add(new PlannedMove
                {
                    ItemGuid = item.Guid,
                    From = item.Position,
                    FromZ = item.Z,
                    To = destWorld,
                    ToZ = d.Z
                });
                plannedCount++;
            }
        }
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
        // For now, scan all chunks for any ChunkStockpileData and gather unique zone IDs
        var zones = new Dictionary<int, StockpileZone>();
        var stockpileManager = new StockpileManager(); // Placeholder if no global manager; will collect from chunks

        // Collect by visiting chunks (zones created via UI live in StockpileManager inside UI; here we reconstruct shards)
        foreach (var chunk in _world.GetAllChunks())
        {
            var stock = chunk.GetStockpileData();
            if (stock == null) continue;
            foreach (var shard in stock.GetAllShards())
            {
                if (!zones.ContainsKey(shard.ZoneId))
                {
                    // Create a temporary zone with this shard only (v1)
                    var z = new StockpileZone(shard.ZoneId, $"Zone {shard.ZoneId}", chunk.Key, 0);
                    z.UpdateMemberChunks(new[] { shard.ChunkKey });
                    zones[shard.ZoneId] = z;
                }
                else
                {
                    var z = zones[shard.ZoneId];
                    var members = z.MemberChunks.ToList();
                    if (!members.Contains(shard.ChunkKey))
                    {
                        members.Add(shard.ChunkKey);
                        z.UpdateMemberChunks(members);
                    }
                }
            }
        }

        return zones.Values.ToList();
    }

    private bool TryFindDestination(Items.ItemInstance item, List<StockpileZone> zones, out Point destWorld)
    {
        // v1: pick the member cell in the nearest zone by Manhattan distance (chunk-level) and use the first cell in that shard
        destWorld = default;

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
                if (ck.Z != item.Z) continue;
                int dist = Math.Abs(ck.ChunkX - itemChunkKey.ChunkX) + Math.Abs(ck.ChunkY - itemChunkKey.ChunkY);
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
            if (ck.Z != item.Z) continue;
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
