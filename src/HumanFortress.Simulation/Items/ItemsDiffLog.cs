using System.Collections.Generic;
using System.Threading;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Items;

/// <summary>
/// Thread-safe log for ItemsDiff operations per tick.
/// </summary>
public sealed class ItemsDiffLog
{
    private readonly List<ItemsDiff> _ops = new();
    private int _localSeq = 0;
    private readonly object _lock = new();

    public void Add(ItemsDiffOp op, ChunkKey chunk, int localIndex, string itemId, int quantity, int priority, string systemId)
    {
        lock (_lock)
        {
            var diff = new ItemsDiff(op, chunk, localIndex, itemId, quantity, priority, systemId, _localSeq++);
            _ops.Add(diff);
        }
    }

    public IReadOnlyList<ItemsDiff> MergeAndSort()
    {
        lock (_lock)
        {
            _ops.Sort((a, b) => a.GetSortKey().CompareTo(b.GetSortKey()));
            return new List<ItemsDiff>(_ops);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _ops.Clear();
            _localSeq = 0;
        }
    }
}

