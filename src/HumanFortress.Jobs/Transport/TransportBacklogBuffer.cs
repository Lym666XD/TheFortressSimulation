using System.Collections.Concurrent;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal sealed class TransportBacklogBuffer
{
    private readonly ConcurrentQueue<TransportRequest> _queue = new();
    private readonly HashSet<Guid> _ids = new();
    private readonly Dictionary<Guid, ulong> _enqueueTick = new();

    internal int Count => _queue.Count;

    internal void DrainInto(int intakeBudget, IList<TransportRequest> into)
    {
        while (into.Count < intakeBudget && _queue.TryDequeue(out var retry))
        {
            into.Add(retry);
            _ids.Remove(retry.ItemGuid);
            _enqueueTick.Remove(retry.ItemGuid);
        }
    }

    internal bool TryEnqueue(in TransportRequest request, ulong tick)
    {
        if (!_ids.Add(request.ItemGuid)) return false;

        _queue.Enqueue(request);
        _enqueueTick[request.ItemGuid] = tick;
        return true;
    }

    internal void EnqueueRange(IReadOnlyList<TransportRequest> requests, int startIndex, ulong tick)
    {
        for (int i = startIndex; i < requests.Count; i++)
        {
            TryEnqueue(requests[i], tick);
        }
    }

    internal int CountOlderThan(ulong tick, int maxAgeTicks)
    {
        int count = 0;
        foreach (var kv in _enqueueTick)
        {
            var age = (int)(tick - kv.Value);
            if (age > maxAgeTicks) count++;
        }
        return count;
    }

    internal IReadOnlyList<TransportBacklogEntrySnapshot> GetStateSnapshot()
    {
        var requests = _queue.ToArray();
        var snapshot = new TransportBacklogEntrySnapshot[requests.Length];
        for (var i = 0; i < requests.Length; i++)
        {
            var request = requests[i];
            snapshot[i] = new TransportBacklogEntrySnapshot(
                i,
                request,
                _enqueueTick.TryGetValue(request.ItemGuid, out var enqueuedTick) ? enqueuedTick : 0UL);
        }

        return snapshot;
    }
}

internal readonly record struct TransportBacklogEntrySnapshot(
    int Order,
    TransportRequest Request,
    ulong EnqueuedTick);
