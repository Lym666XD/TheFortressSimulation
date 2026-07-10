using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Simulation;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Jobs
{
    /// <summary>
    /// Classification of transport intents. Used for priority policies, destination validation, and diagnostics.
    /// The executor uses this to determine which validation logic to apply at the destination.
    /// </summary>
    internal enum TransportReason
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
    internal readonly record struct TransportRequest(
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
    internal interface ITransportIntake
    {
        /// <summary>
        /// Enqueue a request if it is not already represented by a pending request for the same item.
        /// Returns true only when a new pending queue entry was added.
        /// </summary>
        bool Enqueue(in TransportRequest request);
    }

    /// <summary>
    /// Drainable queue interface for the executor. Thread-safe.
    /// </summary>
    internal interface ITransportRequestQueue : ITransportIntake
    {
        int Drain(int max, IList<TransportRequest> into);
        /// <summary>Peek up to max pending requests in stable order without dequeuing.</summary>
        IReadOnlyList<TransportRequest> Peek(int max);
        /// <summary>Get all pending requests in stable order without dequeuing.</summary>
        TransportRequestQueueStateSnapshot GetStateSnapshot();
        /// <summary>Restore pending requests from a validated replay/save snapshot.</summary>
        void RestoreStateSnapshot(TransportRequestQueueStateSnapshot snapshot);
        /// <summary>Get current shard counts keyed by encoded chunk id.</summary>
        IReadOnlyDictionary<int, int> GetShardCountsSnapshot();
        int Count { get; }
    }

    internal readonly record struct TransportRequestQueueStateSnapshot(
        IReadOnlyList<TransportRequest> PendingRequests);

    internal sealed class TransportRequestComparer : IComparer<TransportRequest>
    {
        internal int Compare(TransportRequest a, TransportRequest b)
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

        int IComparer<TransportRequest>.Compare(TransportRequest a, TransportRequest b)
        {
            return Compare(a, b);
        }
    }

    /// <summary>
    /// Thread-safe transport request queue.
    /// Design: multi-producer/single-consumer typical pattern. We keep an internal list guarded by a lock
    /// and sort upon drain to ensure stable ordering across platforms/threads.
    /// </summary>
    internal sealed class TransportRequestQueue : ITransportRequestQueue
    {
        private readonly object _lock = new();
        private readonly List<TransportRequest> _pending = new();
        private readonly TransportRequestComparer _cmp = new();
        private readonly Dictionary<int, List<TransportRequest>> _shards = new();

        // Simple counters for diagnostics
        private int _enqueuedTotal;
        private int _droppedTotal;

        internal int Count
        {
            get { lock (_lock) return _pending.Count; }
        }

        int ITransportRequestQueue.Count => Count;

        internal bool Enqueue(in TransportRequest request)
        {
            lock (_lock)
            {
                // Keep one pending transport intent per item. Same-destination requests can merge
                // quantities; competing destinations preserve the earlier deterministic request.
                for (int i = 0; i < _pending.Count; i++)
                {
                    var r = _pending[i];
                    if (r.ItemGuid != request.ItemGuid)
                        continue;

                    if (r.To == request.To && r.ToZ == request.ToZ)
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
                        ReplaceShardRequest(r, merged);
                        _pending[i] = merged;
                        _droppedTotal++;
                        return false;
                    }

                    _droppedTotal++;
                    return false;
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
                _enqueuedTotal++;
                return true;
            }
        }

        bool ITransportIntake.Enqueue(in TransportRequest request)
        {
            return Enqueue(in request);
        }

        internal int Drain(int max, IList<TransportRequest> into)
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

        int ITransportRequestQueue.Drain(int max, IList<TransportRequest> into)
        {
            return Drain(max, into);
        }

        internal IReadOnlyList<TransportRequest> Peek(int max)
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

        IReadOnlyList<TransportRequest> ITransportRequestQueue.Peek(int max)
        {
            return Peek(max);
        }

        internal TransportRequestQueueStateSnapshot GetStateSnapshot()
        {
            lock (_lock)
            {
                if (_pending.Count == 0)
                    return new TransportRequestQueueStateSnapshot(Array.Empty<TransportRequest>());

                var snapshot = new List<TransportRequest>(_pending);
                snapshot.Sort(_cmp);
                return new TransportRequestQueueStateSnapshot(snapshot.ToArray());
            }
        }

        TransportRequestQueueStateSnapshot ITransportRequestQueue.GetStateSnapshot()
        {
            return GetStateSnapshot();
        }

        internal void RestoreStateSnapshot(TransportRequestQueueStateSnapshot snapshot)
        {
            lock (_lock)
            {
                _pending.Clear();
                _shards.Clear();
                _enqueuedTotal = 0;
                _droppedTotal = 0;

                var pending = snapshot.PendingRequests ?? Array.Empty<TransportRequest>();
                foreach (var request in pending.OrderBy(static request => request.CreatedTick)
                             .ThenBy(static request => request.Priority)
                             .ThenBy(static request => request.RequestorId, StringComparer.Ordinal)
                             .ThenBy(static request => request.ItemGuid))
                {
                    _pending.Add(request);
                    int shardId = EncodeChunkIdFromTo(request.To.X, request.To.Y, request.ToZ);
                    if (!_shards.TryGetValue(shardId, out var list))
                    {
                        list = new List<TransportRequest>();
                        _shards[shardId] = list;
                    }

                    list.Add(request);
                    _enqueuedTotal++;
                }
            }
        }

        void ITransportRequestQueue.RestoreStateSnapshot(TransportRequestQueueStateSnapshot snapshot)
        {
            RestoreStateSnapshot(snapshot);
        }

        internal IReadOnlyDictionary<int, int> GetShardCountsSnapshot()
        {
            lock (_lock)
            {
                var dict = new Dictionary<int, int>(_shards.Count);
                foreach (var kv in _shards.OrderBy(static kv => kv.Key))
                {
                    dict[kv.Key] = kv.Value.Count;
                }
                return dict;
            }
        }

        IReadOnlyDictionary<int, int> ITransportRequestQueue.GetShardCountsSnapshot()
        {
            return GetShardCountsSnapshot();
        }

        // === Chunk-sharding helpers (v2; not used by executor yet) ===
        internal int[] GetActiveShardIds()
        {
            lock (_lock)
            {
                return _shards
                    .OrderBy(static kv => kv.Key)
                    .Select(static kv => kv.Key)
                    .ToArray();
            }
        }

        internal int GetShardCount(int shardId)
        {
            lock (_lock)
            {
                return _shards.TryGetValue(shardId, out var list) ? list.Count : 0;
            }
        }

        internal int DrainByShard(int shardId, int max, IList<TransportRequest> into)
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
            int chunkX = worldX / DiffTargetEncoding.ChunkSizeXY;
            int chunkY = worldY / DiffTargetEncoding.ChunkSizeXY;
            return DiffTargetEncoding.EncodeChunkId(chunkX, chunkY, z);
        }

        private void ReplaceShardRequest(TransportRequest existing, TransportRequest replacement)
        {
            int shardId = EncodeChunkIdFromTo(existing.To.X, existing.To.Y, existing.ToZ);
            if (!_shards.TryGetValue(shardId, out var list))
                return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Equals(existing))
                {
                    list[i] = replacement;
                    return;
                }
            }
        }
    }
}
