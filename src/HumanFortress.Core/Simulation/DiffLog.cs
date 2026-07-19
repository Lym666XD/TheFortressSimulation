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
    public readonly ushort SystemOrder;
    public readonly int LocalSeq;
    public readonly ulong Args; // Packed arguments
    public readonly ulong SortKey;

    public DiffOp(
        DiffOpType op,
        DiffTarget target,
        string systemId,
        int priority,
        ulong args = 0,
        ushort systemOrder = ushort.MaxValue,
        int localSeq = 0)
    {
        Op = op;
        Target = target;
        SystemId = systemId;
        Priority = priority;
        SystemOrder = systemOrder;
        LocalSeq = localSeq;
        Args = args;
        SortKey = BuildSortKey(op, target, priority);
    }

    /// <summary>
    /// Stable sort key for deterministic merge order.
    /// </summary>
    private static ulong BuildSortKey(DiffOpType op, DiffTarget target, int priority)
    {
        return ((ulong)(uint)target.ChunkId << 32) |
               ((ulong)(ushort)target.LocalIndex << 16) |
               ((ulong)(byte)priority << 8) |
               (byte)op;
    }
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
    public readonly ulong EntityKey; // Wider stable key for entity-scoped diff merge/apply
    public readonly bool HasEntityKey;

    public DiffTarget(int chunkId, int localIndex, int entityId = -1)
        : this(chunkId, localIndex, entityId, entityKey: 0, hasEntityKey: false)
    {
    }

    public DiffTarget(int chunkId, int localIndex, int entityId, ulong entityKey)
        : this(chunkId, localIndex, entityId, entityKey, hasEntityKey: true)
    {
    }

    private DiffTarget(int chunkId, int localIndex, int entityId, ulong entityKey, bool hasEntityKey)
    {
        ChunkId = chunkId;
        LocalIndex = localIndex;
        EntityId = entityId;
        EntityKey = entityKey;
        HasEntityKey = hasEntityKey;
    }

    public readonly ulong EffectiveEntityKey =>
        HasEntityKey
            ? EntityKey
            : EntityId == -1
                ? ulong.MaxValue
                : unchecked((uint)EntityId);
}

/// <summary>
/// Collects and merges diff operations for a single tick.
/// </summary>
public sealed partial class DiffLog
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
            _operations.Sort(CompareOps);

            // Resolve conflicts
            var merged = new List<DiffOp>();

            // For entity-scoped ops, merge by operation + effective entity key so multiple entities can share a tile.
            var entityOps = new Dictionary<(DiffOpType Op, ulong EntityKey), int>();

            // For other ops, track last op by (chunkId, localIndex, op) to apply priority rule
            var lastByTarget = new Dictionary<(int,int,DiffOpType), int>();

            for (int i = 0; i < _operations.Count; i++)
            {
                var op = _operations[i];
                if (IsEntityScoped(op))
                {
                    var entityKey = (op.Op, op.Target.EffectiveEntityKey);
                    if (entityOps.TryGetValue(entityKey, out int idx))
                    {
                        var incumbent = merged[idx];
                        if (IsBetter(op, incumbent))
                        {
                            merged[idx] = op;
                        }
                        // else drop
                    }
                    else
                    {
                        entityOps[entityKey] = merged.Count;
                        merged.Add(op);
                    }
                    continue;
                }

                var key = (op.Target.ChunkId, op.Target.LocalIndex, op.Op);
                if (lastByTarget.TryGetValue(key, out int prevIdx))
                {
                    var prev = merged[prevIdx];
                    if (IsBetter(op, prev))
                    {
                        merged[prevIdx] = op;
                    }
                    // else keep previous
                }
                else
                {
                    lastByTarget[key] = merged.Count;
                    merged.Add(op);
                }
            }

            return merged;
        }
    }

    private static bool IsEntityScoped(DiffOp op)
    {
        return op.Op == DiffOpType.MoveCreature
            || op.Op == DiffOpType.MoveItem
            || op.Op == DiffOpType.MarkCarried
            || op.Op == DiffOpType.UnmarkCarried;
    }

    private static bool IsBetter(in DiffOp candidate, in DiffOp incumbent)
    {
        // Lower Priority value means higher priority in this codebase
        if (candidate.Priority != incumbent.Priority)
            return candidate.Priority < incumbent.Priority;

        if (candidate.SystemOrder != incumbent.SystemOrder)
            return candidate.SystemOrder < incumbent.SystemOrder;

        // Fallback: later deterministic ordering wins to keep behavior similar to previous last-writer-wins.
        return CompareOps(candidate, incumbent) >= 0;
    }

    private static int CompareOps(DiffOp a, DiffOp b)
    {
        int c = a.Target.ChunkId.CompareTo(b.Target.ChunkId);
        if (c != 0) return c;

        c = a.Target.LocalIndex.CompareTo(b.Target.LocalIndex);
        if (c != 0) return c;

        // Lower numeric priority is authoritative everywhere. Do not use the
        // legacy packed SortKey here: it truncates priority to eight bits.
        c = a.Priority.CompareTo(b.Priority);
        if (c != 0) return c;

        c = a.Op.CompareTo(b.Op);
        if (c != 0) return c;

        c = a.Target.EffectiveEntityKey.CompareTo(b.Target.EffectiveEntityKey);
        if (c != 0) return c;

        c = a.Target.EntityId.CompareTo(b.Target.EntityId);
        if (c != 0) return c;

        c = a.SystemOrder.CompareTo(b.SystemOrder);
        if (c != 0) return c;

        c = string.CompareOrdinal(a.SystemId, b.SystemId);
        if (c != 0) return c;

        c = a.LocalSeq.CompareTo(b.LocalSeq);
        if (c != 0) return c;

        return a.Args.CompareTo(b.Args);
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
