using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Jobs
{
    /// <summary>
    /// Classification of transport intents. Used for priority policies and diagnostics.
    /// </summary>
    public enum TransportReason
    {
        ToStockpile,
        ToConstructionSite,
        ToInstallSite,
        ToWorkshopInput,
        ToWorkshopOutput,
        Cleanup,
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
