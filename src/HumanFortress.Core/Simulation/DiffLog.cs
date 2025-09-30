namespace HumanFortress.Core.Simulation;

/// <summary>
/// Diff-log operation for tile/entity modifications per DIFF_LOG_AND_MERGE_STRATEGIES.md.
/// All write operations must go through this system for determinism.
/// </summary>
public readonly struct DiffOp
{
    public readonly DiffOpType Op;
    public readonly DiffTarget Target;
    public readonly string SystemId;
    public readonly int Priority;
    public readonly ulong Args; // Packed arguments

    public DiffOp(DiffOpType op, DiffTarget target, string systemId, int priority, ulong args = 0)
    {
        Op = op;
        Target = target;
        SystemId = systemId;
        Priority = priority;
        Args = args;
    }

    /// <summary>
    /// Stable sort key for deterministic merge order.
    /// </summary>
    public readonly ulong SortKey =>
        ((ulong)Target.ChunkId << 32) |
        ((ulong)Target.LocalIndex << 16) |
        ((ulong)(uint)Priority << 8) |
        ((ulong)(uint)SystemId.GetHashCode());
}

public enum DiffOpType : byte
{
    SetTerrain,
    SetFluid,
    AddField,
    RemoveField,
    PlaceFurniture,
    RemoveFurniture,
    AddItem,
    RemoveItem,
    MoveCreature,
    DamageCreature,
    ModifyAttribute,
    // Items/Jobs v1.1 extensions (used by Haul jobs)
    MoveItem,
    MarkCarried,
    UnmarkCarried
}

public readonly struct DiffTarget
{
    public readonly int ChunkId;
    public readonly int LocalIndex; // Within chunk (0-1023 for 32x32)
    public readonly int EntityId; // For entity operations

    public DiffTarget(int chunkId, int localIndex, int entityId = -1)
    {
        ChunkId = chunkId;
        LocalIndex = localIndex;
        EntityId = entityId;
    }
}

/// <summary>
/// Collects and merges diff operations for a single tick.
/// </summary>
public sealed class DiffLog
{
    private readonly List<DiffOp> _operations = new();
    private readonly object _lock = new();

    /// <summary>
    /// Add an operation to the log. Thread-safe for Write phase.
    /// </summary>
    public void AddOp(DiffOp op)
    {
        lock (_lock)
        {
            _operations.Add(op);
        }
    }

    /// <summary>
    /// Merge and apply all operations in deterministic order.
    /// </summary>
    public IReadOnlyList<DiffOp> MergeAndSort()
    {
        lock (_lock)
        {
            // Sort by stable key for determinism
            _operations.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));

            // Resolve conflicts (last writer wins within same target)
            var merged = new List<DiffOp>();
            DiffOp? lastOp = null;

            foreach (var op in _operations)
            {
                if (lastOp.HasValue &&
                    op.Target.ChunkId == lastOp.Value.Target.ChunkId &&
                    op.Target.LocalIndex == lastOp.Value.Target.LocalIndex &&
                    op.Op == lastOp.Value.Op)
                {
                    // Same operation on same target - keep higher priority
                    if (op.Priority <= lastOp.Value.Priority)
                    {
                        merged[merged.Count - 1] = op;
                    }
                }
                else
                {
                    merged.Add(op);
                }
                lastOp = op;
            }

            return merged;
        }
    }

    /// <summary>
    /// Clear the log for the next tick.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _operations.Clear();
        }
    }
}
