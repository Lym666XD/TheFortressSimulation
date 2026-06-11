using System.Collections.Concurrent;
using HumanFortress.Simulation.Orders;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningBacklogBuffer
{
    private readonly ConcurrentQueue<BacklogEntry> _queue = new();

    public int Count => _queue.Count;

    public void DrainInto(int intakeBudget, IList<MiningSystem.PlannedDig> into)
    {
        while (into.Count < intakeBudget && _queue.TryDequeue(out var retry))
        {
            into.Add(retry.Dig);
        }
    }

    public void Enqueue(in MiningSystem.PlannedDig dig, ulong tick)
    {
        _queue.Enqueue(new BacklogEntry(dig, tick));
    }

    public int CountOlderThan(ulong tick, int maxAgeTicks)
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

    private readonly record struct BacklogEntry(MiningSystem.PlannedDig Dig, ulong EnqueueTick);
}
