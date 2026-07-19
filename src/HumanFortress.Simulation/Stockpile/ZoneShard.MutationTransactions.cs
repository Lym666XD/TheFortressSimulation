namespace HumanFortress.Simulation.Stockpile;

internal sealed partial class ZoneShard
{
    internal readonly record struct MutationMemento(
        int ZoneId,
        HumanFortress.Simulation.World.ChunkKey ChunkKey,
        IReadOnlyList<int> Cells,
        int UsedSlots,
        int ReservedSlots,
        int IncomingCount);

    internal MutationMemento CaptureMutationMemento()
    {
        return new MutationMemento(
            ZoneId,
            ChunkKey,
            Enumerable.Range(0, HumanFortress.Simulation.World.Chunk.CELLS_PER_LAYER)
                .Where(ContainsCell)
                .ToArray(),
            UsedSlots,
            ReservedSlots,
            IncomingCount);
    }

    internal static ZoneShard RestoreMutationMemento(MutationMemento memento)
    {
        var shard = new ZoneShard(memento.ZoneId, memento.ChunkKey);
        shard.AddCells(memento.Cells);
        shard.UsedSlots = memento.UsedSlots;
        shard.ReservedSlots = memento.ReservedSlots;
        shard.IncomingCount = memento.IncomingCount;
        return shard;
    }
}
