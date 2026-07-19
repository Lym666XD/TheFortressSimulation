using HumanFortress.Simulation.Orders;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningBacklogBuffer
{
    private readonly Queue<BacklogEntry> _queue = new();

    internal int Count => _queue.Count;

    internal void DrainInto(int intakeBudget, IList<MiningSystem.PlannedDig> into)
    {
        while (into.Count < intakeBudget && _queue.TryDequeue(out var retry))
        {
            into.Add(retry.Dig);
        }
    }

    internal void Enqueue(in MiningSystem.PlannedDig dig, ulong tick)
    {
        _queue.Enqueue(new BacklogEntry(dig, tick));
    }

    internal int CountOlderThan(ulong tick, int maxAgeTicks)
    {
        int count = 0;
        foreach (var entry in _queue.ToArray())
        {
            if (tick > entry.EnqueueTick && (tick - entry.EnqueueTick) >= (ulong)maxAgeTicks)
            {
                count++;
            }
        }

        return count;
    }

    internal IReadOnlyList<MiningBacklogEntrySnapshot> GetStateSnapshot()
    {
        var entries = _queue.ToArray();
        var snapshot = new MiningBacklogEntrySnapshot[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            snapshot[i] = new MiningBacklogEntrySnapshot(i, entries[i].Dig, entries[i].EnqueueTick);
        }

        return snapshot;
    }

    internal void RestoreStateSnapshot(IEnumerable<MiningBacklogEntrySnapshot> entries)
    {
        _queue.Clear();

        foreach (var entry in entries.OrderBy(static entry => entry.Order))
            _queue.Enqueue(new BacklogEntry(entry.Dig, entry.EnqueuedTick));
    }

    private readonly record struct BacklogEntry(MiningSystem.PlannedDig Dig, ulong EnqueueTick);
}

internal readonly record struct MiningBacklogEntrySnapshot(
    int Order,
    MiningSystem.PlannedDig Dig,
    ulong EnqueuedTick);
