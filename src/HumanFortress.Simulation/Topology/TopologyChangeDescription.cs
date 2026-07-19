using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Topology;

internal enum TopologyChangeKind : byte
{
    Terrain = 0,
    PlaceableCreate = 1,
    PlaceableRemove = 2,
    DoorState = 3,
}

/// <summary>
/// Immutable description of the chunks and direct cells whose navigation
/// dependencies are changed by one Simulation-owned commit.
/// </summary>
internal sealed class TopologyChangeDescription
{
    internal TopologyChangeDescription(
        TopologyChangeKind kind,
        Guid? subjectId,
        IReadOnlyList<TopologyAffectedChunk> affectedChunks)
    {
        Kind = kind;
        SubjectId = subjectId;
        AffectedChunks = affectedChunks.ToArray();
    }

    internal TopologyChangeKind Kind { get; }

    internal Guid? SubjectId { get; }

    internal IReadOnlyList<TopologyAffectedChunk> AffectedChunks { get; }
}

internal readonly record struct TopologyAffectedChunk(
    ChunkKey Chunk,
    IReadOnlyList<int> ChangedLocalIndexes);
