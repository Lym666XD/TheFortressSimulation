using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal sealed class TransportBacklogBuffer
{
    private readonly Queue<TransportRequest> _queue = new();
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

    internal bool TryConsume(in TransportRequest expected, ulong expectedEnqueuedTick)
    {
        if (!_ids.Contains(expected.ItemGuid)
            || !_enqueueTick.TryGetValue(expected.ItemGuid, out var enqueuedTick)
            || enqueuedTick != expectedEnqueuedTick)
        {
            return false;
        }

        var retained = new Queue<TransportRequest>();
        bool removed = false;
        while (_queue.TryDequeue(out var request))
        {
            if (!removed && request.Equals(expected))
            {
                removed = true;
                continue;
            }

            retained.Enqueue(request);
        }

        while (retained.TryDequeue(out var request))
            _queue.Enqueue(request);

        if (!removed)
            return false;

        _ids.Remove(expected.ItemGuid);
        _enqueueTick.Remove(expected.ItemGuid);
        return true;
    }

    internal int EnqueueRange(IReadOnlyList<TransportRequest> requests, int startIndex, ulong tick)
    {
        var enqueued = 0;
        for (int i = startIndex; i < requests.Count; i++)
        {
            if (TryEnqueue(requests[i], tick))
            {
                enqueued++;
            }
        }

        return enqueued;
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

    internal void RestoreStateSnapshot(IReadOnlyList<TransportBacklogEntrySnapshot> entries)
    {
        _ids.Clear();
        _enqueueTick.Clear();
        _queue.Clear();

        foreach (var entry in entries.OrderBy(static entry => entry.Order))
        {
            if (!_ids.Add(entry.Request.ItemGuid))
                continue;

            _queue.Enqueue(entry.Request);
            _enqueueTick[entry.Request.ItemGuid] = entry.EnqueuedTick;
        }
    }
}

internal readonly record struct TransportBacklogEntrySnapshot(
    int Order,
    TransportRequest Request,
    ulong EnqueuedTick);
