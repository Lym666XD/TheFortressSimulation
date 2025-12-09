using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Jobs
{
    /// <summary>
    /// Classification of transport intents. Used for priority policies, destination validation, and diagnostics.
    /// The executor uses this to determine which validation logic to apply at the destination.
    /// </summary>
    public enum TransportReason
    {
        // === Stockpile Operations ===
        /// <summary>Hauling loose items to a stockpile zone. Validates destination is a stockpile cell.</summary>
        ToStockpile,

        // === Construction Operations ===
        /// <summary>Delivering materials to a construction site (L0 walls/floors or L2 workshops). Validates destination is near a construction site.</summary>
        ToConstructionSite,
        /// <summary>Delivering items for installation (furniture, machines). Validates destination is an install site.</summary>
        ToInstallSite,

        // === Workshop/Crafting Operations ===
        /// <summary>Delivering raw materials to a workshop input buffer for crafting. Validates destination is a workshop.</summary>
        ToWorkshopInput,
        /// <summary>Moving finished goods from workshop output to stockpile or trade depot. Validates source is a workshop.</summary>
        ToWorkshopOutput,
        /// <summary>Delivering materials for upgrading an existing structure/workshop. Validates destination has upgradeable placeable.</summary>
        ToUpgradeSite,

        // === Trade Operations ===
        /// <summary>Moving goods to a trade depot for export/sale. Validates destination is a trade depot zone.</summary>
        ToTradeDepot,
        /// <summary>Moving purchased goods from trade depot to stockpile. Validates source is a trade depot.</summary>
        FromTradeDepot,

        // === Military/Equipment Operations ===
        /// <summary>Delivering equipment to an armory or barracks for military use. Validates destination is military zone.</summary>
        ToArmory,
        /// <summary>Delivering ammunition to defensive positions or ammo stockpiles. Validates destination accepts ammo.</summary>
        ToAmmoCache,

        // === Maintenance Operations ===
        /// <summary>Cleaning up debris, corpses, or garbage. Less strict destination validation.</summary>
        Cleanup,
        /// <summary>Refueling torches, furnaces, or other fuel-consuming structures. Validates destination needs fuel.</summary>
        ToRefuel,

        // === Miscellaneous ===
        /// <summary>Generic transport with minimal validation. Use sparingly.</summary>
        Misc
    }

    /// <summary>
    /// A single transport request: move an item from (From,FromZ) to (To,ToZ).
    /// This is a read-only planning artifact; executors must revalidate state on pickup.
    /// </summary>
    public readonly record struct TransportRequest(
        Guid ItemGuid,
        Point From,
        int FromZ,
        Point To,
        int ToZ,
        int Quantity, // number of units to move from stack
        TransportReason Reason,
        int Priority,
        string RequestorId,
        ulong CreatedTick,
        uint Seed);

    /// <summary>
    /// Intake interface for producers (construction/workshop/install/stockpile planners).
    /// </summary>
    public interface ITransportIntake
    {
        void Enqueue(in TransportRequest request);
    }

    /// <summary>
    /// Drainable queue interface for the executor. Thread-safe.
    /// </summary>
    public interface ITransportRequestQueue : ITransportIntake
    {
        int Drain(int max, IList<TransportRequest> into);
        /// <summary>Peek up to max pending requests in stable order without dequeuing.</summary>
        IReadOnlyList<TransportRequest> Peek(int max);
        /// <summary>Get current shard counts keyed by encoded chunk id.</summary>
        IReadOnlyDictionary<int, int> GetShardCountsSnapshot();
        int Count { get; }
    }

    internal sealed class TransportRequestComparer : IComparer<TransportRequest>
    {
        public int Compare(TransportRequest a, TransportRequest b)
        {
            // Stable order: CreatedTick → Priority (asc) → RequestorId → ItemGuid
            int c = a.CreatedTick.CompareTo(b.CreatedTick);
            if (c != 0) return c;
            c = a.Priority.CompareTo(b.Priority);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.RequestorId, b.RequestorId);
            if (c != 0) return c;
            return a.ItemGuid.CompareTo(b.ItemGuid);
        }
    }

    /// <summary>
    /// Thread-safe transport request queue.
    /// Design: multi-producer/single-consumer typical pattern. We keep an internal list guarded by a lock
    /// and sort upon drain to ensure stable ordering across platforms/threads.
    /// </summary>
    public sealed class TransportRequestQueue : ITransportRequestQueue
    {
        private readonly object _lock = new();
        private readonly List<TransportRequest> _pending = new();
        private readonly TransportRequestComparer _cmp = new();
        private readonly Dictionary<int, List<TransportRequest>> _shards = new();

        // Simple counters for diagnostics
        private int _enqueuedTotal;
        private int _droppedTotal;

        public int Count
        {
            get { lock (_lock) return _pending.Count; }
        }

        public void Enqueue(in TransportRequest request)
        {
            lock (_lock)
            {
                // Optional: simple de-dup/merge by (ItemGuid,To,ToZ) to avoid thrash
                for (int i = 0; i < _pending.Count; i++)
                {
                    var r = _pending[i];
                    if (r.ItemGuid == request.ItemGuid && r.To == request.To && r.ToZ == request.ToZ)
                    {
                        // Merge quantities to a single request, keep earlier CreatedTick
                        var merged = new TransportRequest(
                            r.ItemGuid,
                            r.From,
                            r.FromZ,
                            r.To,
                            r.ToZ,
                            r.Quantity + Math.Max(0, request.Quantity),
                            r.Reason,
                            System.Math.Min(r.Priority, request.Priority),
                            r.RequestorId,
                            r.CreatedTick,
                            r.Seed);
                        _pending[i] = merged;
                        _droppedTotal++;
                        return;
                    }
                }
                _pending.Add(request);
                // Shard by destination chunk (derive chunk id from To/ToZ)
                int shardId = EncodeChunkIdFromTo(request.To.X, request.To.Y, request.ToZ);
                if (!_shards.TryGetValue(shardId, out var list))
                {
                    list = new List<TransportRequest>();
                    _shards[shardId] = list;
                }
                list.Add(request);
                Interlocked.Increment(ref _enqueuedTotal);
            }
        }

        public int Drain(int max, IList<TransportRequest> into)
        {
            if (max <= 0) return 0;
            lock (_lock)
            {
                if (_pending.Count == 0) return 0;
                // Sort to enforce stable deterministic order
                _pending.Sort(_cmp);
                int take = Math.Min(max, _pending.Count);
                for (int i = 0; i < take; i++)
                {
                    var rq = _pending[i];
                    into.Add(rq);
                    // Remove from shard too
                    int shardId = EncodeChunkIdFromTo(rq.To.X, rq.To.Y, rq.ToZ);
                    if (_shards.TryGetValue(shardId, out var list))
                    {
                        for (int j = 0; j < list.Count; j++)
                        {
                            if (list[j].Equals(rq)) { list.RemoveAt(j); break; }
                        }
                        if (list.Count == 0) _shards.Remove(shardId);
                    }
                }
                _pending.RemoveRange(0, take);
                return take;
            }
        }

        public IReadOnlyList<TransportRequest> Peek(int max)
        {
            if (max <= 0) return Array.Empty<TransportRequest>();
            lock (_lock)
            {
                if (_pending.Count == 0) return Array.Empty<TransportRequest>();
                var snapshot = new List<TransportRequest>(_pending);
                snapshot.Sort(_cmp);
                if (snapshot.Count > max) snapshot.RemoveRange(max, snapshot.Count - max);
                return snapshot;
            }
        }

        public IReadOnlyDictionary<int, int> GetShardCountsSnapshot()
        {
            lock (_lock)
            {
                var dict = new Dictionary<int, int>(_shards.Count);
                foreach (var kv in _shards)
                {
                    dict[kv.Key] = kv.Value.Count;
                }
                return dict;
            }
        }

        // === Chunk-sharding helpers (v2; not used by executor yet) ===
        public int[] GetActiveShardIds()
        {
            lock (_lock)
            {
                return _shards.Keys.ToArray();
            }
        }

        public int GetShardCount(int shardId)
        {
            lock (_lock)
            {
                return _shards.TryGetValue(shardId, out var list) ? list.Count : 0;
            }
        }

        public int DrainByShard(int shardId, int max, IList<TransportRequest> into)
        {
            lock (_lock)
            {
                if (!_shards.TryGetValue(shardId, out var list) || list.Count == 0) return 0;
                // Local stable order by same comparer
                list.Sort(_cmp);
                int take = Math.Min(max, list.Count);
                for (int i = 0; i < take; i++)
                {
                    var rq = list[i];
                    into.Add(rq);
                    // Also remove one occurrence from global pending
                    for (int j = 0; j < _pending.Count; j++)
                    {
                        if (_pending[j].Equals(rq)) { _pending.RemoveAt(j); break; }
                    }
                }
                list.RemoveRange(0, take);
                if (list.Count == 0) _shards.Remove(shardId);
                return take;
            }
        }

        private static int EncodeChunkIdFromTo(int worldX, int worldY, int z)
        {
            const int SIZE_XY = 32;
            int cx = worldX / SIZE_XY;
            int cy = worldY / SIZE_XY;
            return ((z & 0x3FF) << 20) | ((cx & 0x3FF) << 10) | (cy & 0x3FF);
        }
    }
}
