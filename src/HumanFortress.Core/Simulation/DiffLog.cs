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

            // Resolve conflicts
            var merged = new List<DiffOp>();

            // For MoveCreature, merge by entity id (one op per entity per tick)
            var moveByEntity = new Dictionary<int, int>(); // entityId -> index in merged

            // For other ops, track last op by (chunkId, localIndex, op) to apply priority rule
            var lastByTarget = new Dictionary<(int,int,DiffOpType), int>();

            for (int i = 0; i < _operations.Count; i++)
            {
                var op = _operations[i];
                if (op.Op == DiffOpType.MoveCreature && op.Target.EntityId >= 0)
                {
                    if (moveByEntity.TryGetValue(op.Target.EntityId, out int idx))
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
                        moveByEntity[op.Target.EntityId] = merged.Count;
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

    private static bool IsBetter(in DiffOp candidate, in DiffOp incumbent)
    {
        // Lower Priority value means higher priority in this codebase
        if (candidate.Priority != incumbent.Priority)
            return candidate.Priority < incumbent.Priority;

        int ca = SystemPrecedence(candidate.SystemId);
        int cb = SystemPrecedence(incumbent.SystemId);
        if (ca != cb) return ca < cb;

        // Fallback: later sortkey wins to keep behavior similar to previous last-writer-wins
        return candidate.SortKey >= incumbent.SortKey;
    }

    private static int SystemPrecedence(string systemId)
    {
        // Smaller is stronger
        if (systemId.StartsWith("Jobs.Mining", StringComparison.Ordinal)) return 0;
        if (systemId.StartsWith("Jobs.Haul", StringComparison.Ordinal)) return 1;
        if (systemId.StartsWith("Jobs.Construction", StringComparison.Ordinal)) return 2;
        return 3;
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
