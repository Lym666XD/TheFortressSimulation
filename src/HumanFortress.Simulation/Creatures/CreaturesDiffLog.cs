using SadRogue.Primitives;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Creatures;

internal enum CreaturesDiffOp
{
    SpawnCreature
}

internal readonly struct CreaturesDiff
{
    internal CreaturesDiff(
        CreaturesDiffOp op,
        string creatureId,
        Point worldPos,
        int z,
        string factionId,
        int priority,
        string systemId,
        int localSeq)
    {
        Op = op;
        CreatureId = creatureId;
        WorldPos = worldPos;
        Z = z;
        FactionId = factionId;
        Priority = priority;
        SystemId = systemId;
        LocalSeq = localSeq;
    }

    internal CreaturesDiffOp Op { get; }
    internal string CreatureId { get; }
    internal Point WorldPos { get; }
    internal int Z { get; }
    internal string FactionId { get; }
    internal int Priority { get; }
    internal string SystemId { get; }
    internal int LocalSeq { get; }

    internal long GetSortKey()
    {
        int chunkX = WorldPos.X >= 0 ? WorldPos.X / Chunk.SIZE_XY : 0;
        int chunkY = WorldPos.Y >= 0 ? WorldPos.Y / Chunk.SIZE_XY : 0;
        int localX = WorldPos.X >= 0 ? WorldPos.X % Chunk.SIZE_XY : 0;
        int localY = WorldPos.Y >= 0 ? WorldPos.Y % Chunk.SIZE_XY : 0;
        int localIndex = Chunk.LocalIndex(localX, localY);

        return SimulationDiffSortKeys.ByChunkCellPriorityAscending(
            Z,
            chunkX,
            chunkY,
            localIndex,
            Priority,
            LocalSeq);
    }

    internal static int CompareDeterministic(CreaturesDiff left, CreaturesDiff right)
    {
        int result = left.Z.CompareTo(right.Z);
        if (result != 0) return result;
        result = left.WorldPos.Y.CompareTo(right.WorldPos.Y);
        if (result != 0) return result;
        result = left.WorldPos.X.CompareTo(right.WorldPos.X);
        if (result != 0) return result;
        result = left.Priority.CompareTo(right.Priority);
        if (result != 0) return result;
        result = left.Op.CompareTo(right.Op);
        if (result != 0) return result;
        result = string.Compare(left.SystemId, right.SystemId, StringComparison.Ordinal);
        if (result != 0) return result;
        return left.LocalSeq.CompareTo(right.LocalSeq);
    }
}

internal sealed partial class CreaturesDiffLog
{
    private readonly List<CreaturesDiff> _ops = new();
    private readonly object _lock = new();
    private int _localSeq;

    internal void AddSpawnCreature(string creatureId, Point worldPos, int z, string factionId, int priority, string systemId)
    {
        lock (_lock)
        {
            _ops.Add(new CreaturesDiff(
                CreaturesDiffOp.SpawnCreature,
                creatureId,
                worldPos,
                z,
                factionId,
                priority,
                systemId,
                _localSeq++));
        }
    }

    internal IReadOnlyList<CreaturesDiff> MergeAndSort()
    {
        lock (_lock)
        {
            _ops.Sort(CreaturesDiff.CompareDeterministic);
            return _ops.ToList();
        }
    }

    internal void Clear()
    {
        lock (_lock)
        {
            _ops.Clear();
            _localSeq = 0;
        }
    }
}
