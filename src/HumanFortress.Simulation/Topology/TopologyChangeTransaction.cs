using HumanFortress.Simulation.World;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Topology;

/// <summary>
/// Validate-before-apply topology transaction. Factories validate authoritative
/// ownership/collision state while holding World.TopologyLock, then this type
/// applies the prepared writes and publishes one connectivity/dirty event for
/// each affected chunk.
/// </summary>
internal sealed partial class TopologyChangeTransaction
{
    private readonly SimulationWorld _world;
    private readonly ulong _tick;
    private readonly TopologyChangeKind _kind;
    private readonly Guid? _subjectId;
    private readonly Func<bool> _applyPreparedWrites;
    private readonly SortedDictionary<ChunkKey, SortedSet<int>> _affectedChunks =
        new(ChunkKeySpatialComparer.Instance);
    private bool _committed;

    internal TopologyChangeTransaction(
        SimulationWorld world,
        ulong tick,
        TopologyChangeKind kind,
        Guid? subjectId,
        Func<bool> applyPreparedWrites)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _tick = tick;
        _kind = kind;
        _subjectId = subjectId;
        _applyPreparedWrites = applyPreparedWrites
            ?? throw new ArgumentNullException(nameof(applyPreparedWrites));
    }

    internal TopologyChangeDescription Description => new(
        _kind,
        _subjectId,
        _affectedChunks
            .Select(static entry => new TopologyAffectedChunk(entry.Key, entry.Value.ToArray()))
            .ToArray());

    /// <summary>
    /// Track the directly changed cell and every loaded chunk whose derived
    /// navigation can depend on it. Boundary cells include the adjacent XY
    /// chunks; vertical neighbors are included for ramp/stair dependencies.
    /// </summary>
    internal void TrackChangedCellAndDependencies(int worldX, int worldY, int z)
    {
        var directKey = ToChunkKey(worldX, worldY, z);
        var directIndex = ToLocalIndex(worldX, worldY);
        AddAffectedChunk(directKey, directIndex);

        var localX = PositiveModulo(worldX, Chunk.SIZE_XY);
        var localY = PositiveModulo(worldY, Chunk.SIZE_XY);
        var chunkXs = BoundaryChunkCoordinates(directKey.ChunkX, localX);
        var chunkYs = BoundaryChunkCoordinates(directKey.ChunkY, localY);

        for (var dependentZ = z - 1; dependentZ <= z + 1; dependentZ++)
        {
            if (dependentZ < 0 || dependentZ >= _world.MaxZ)
                continue;

            foreach (var chunkY in chunkYs)
            {
                foreach (var chunkX in chunkXs)
                {
                    var key = new ChunkKey(chunkX, chunkY, dependentZ);
                    if (_world.GetChunk(key) != null)
                        AddAffectedChunk(key, localIndex: null);
                }
            }
        }
    }

    internal TopologyChangeDescription Commit()
    {
        if (_committed)
            throw new InvalidOperationException("Topology transaction has already committed.");

        // Prepared writes use only prevalidated cells and non-throwing dictionary
        // operations. A false result indicates an internal invariant violation,
        // not an expected gameplay rejection.
        if (!_applyPreparedWrites())
            throw new InvalidOperationException("Prepared topology writes no longer match validated state.");

        foreach (var entry in _affectedChunks)
        {
            var chunk = _world.GetChunk(entry.Key)
                ?? throw new InvalidOperationException($"Affected topology chunk {entry.Key} was unloaded during commit.");
            chunk.CommitTopologyChange(entry.Value, _tick);
            _world.MarkChunkDirty(entry.Key);
        }

        _committed = true;
        return Description;
    }

    private void AddAffectedChunk(ChunkKey key, int? localIndex)
    {
        if (!_affectedChunks.TryGetValue(key, out var localIndexes))
        {
            localIndexes = new SortedSet<int>();
            _affectedChunks.Add(key, localIndexes);
        }

        if (localIndex.HasValue)
            localIndexes.Add(localIndex.Value);
    }

    private static ChunkKey ToChunkKey(int worldX, int worldY, int z)
    {
        return new ChunkKey(
            worldX / Chunk.SIZE_XY,
            worldY / Chunk.SIZE_XY,
            z);
    }

    private static int ToLocalIndex(int worldX, int worldY)
    {
        return Chunk.LocalIndex(
            PositiveModulo(worldX, Chunk.SIZE_XY),
            PositiveModulo(worldY, Chunk.SIZE_XY));
    }

    private static int[] BoundaryChunkCoordinates(int chunkCoordinate, int localCoordinate)
    {
        if (localCoordinate == 0)
            return new[] { chunkCoordinate - 1, chunkCoordinate };
        if (localCoordinate == Chunk.SIZE_XY - 1)
            return new[] { chunkCoordinate, chunkCoordinate + 1 };
        return new[] { chunkCoordinate };
    }

    private static int PositiveModulo(int value, int divisor)
    {
        return ((value % divisor) + divisor) % divisor;
    }

    private sealed class ChunkKeySpatialComparer : IComparer<ChunkKey>
    {
        internal static ChunkKeySpatialComparer Instance { get; } = new();

        int IComparer<ChunkKey>.Compare(ChunkKey left, ChunkKey right)
        {
            var result = left.Z.CompareTo(right.Z);
            if (result != 0)
                return result;
            result = left.ChunkY.CompareTo(right.ChunkY);
            return result != 0 ? result : left.ChunkX.CompareTo(right.ChunkX);
        }
    }
}
