using SadRogue.Primitives;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Creatures;

internal enum CreaturesDiffOp
{
    SpawnCreature
}

internal readonly struct CreaturesDiff
{
    public CreaturesDiff(
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

    public CreaturesDiffOp Op { get; }
    public string CreatureId { get; }
    public Point WorldPos { get; }
    public int Z { get; }
    public string FactionId { get; }
    public int Priority { get; }
    public string SystemId { get; }
    public int LocalSeq { get; }

    public long GetSortKey()
    {
        int chunkX = WorldPos.X >= 0 ? WorldPos.X / Chunk.SIZE_XY : 0;
        int chunkY = WorldPos.Y >= 0 ? WorldPos.Y / Chunk.SIZE_XY : 0;
        int localX = WorldPos.X >= 0 ? WorldPos.X % Chunk.SIZE_XY : 0;
        int localY = WorldPos.Y >= 0 ? WorldPos.Y % Chunk.SIZE_XY : 0;
        int localIndex = Chunk.LocalIndex(localX, localY);

        long key = 0;
        key |= ((long)(Z & 0xFF)) << 56;
        key |= ((long)(chunkX & 0xFF)) << 48;
        key |= ((long)(chunkY & 0xFF)) << 40;
        key |= ((long)(localIndex & 0xFFFF)) << 24;
        key |= ((long)(Priority & 0xFF)) << 16;
        key |= (ushort)LocalSeq;
        return key;
    }
}

internal sealed class CreaturesDiffLog
{
    private readonly List<CreaturesDiff> _ops = new();
    private readonly object _lock = new();
    private int _localSeq;

    public void AddSpawnCreature(string creatureId, Point worldPos, int z, string factionId, int priority, string systemId)
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

    public IReadOnlyList<CreaturesDiff> MergeAndSort()
    {
        lock (_lock)
        {
            _ops.Sort((a, b) => a.GetSortKey().CompareTo(b.GetSortKey()));
            return _ops.ToList();
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
