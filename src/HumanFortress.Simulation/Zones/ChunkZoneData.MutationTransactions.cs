namespace HumanFortress.Simulation.Zones;

internal sealed partial class ChunkZoneData
{
    internal readonly record struct ShardMemento(
        int ZoneId,
        HumanFortress.Simulation.World.ChunkKey ChunkKey,
        IReadOnlyList<int> Cells);

    internal readonly record struct MutationMemento(
        IReadOnlyList<ShardMemento> Shards,
        uint DirtyGeneration);

    internal MutationMemento CaptureMutationMemento()
    {
        lock (_readLock)
        {
            return new MutationMemento(
                _shards.Values
                    .OrderBy(static shard => shard.ZoneId)
                    .Select(static shard => new ShardMemento(
                        shard.ZoneId,
                        shard.ChunkKey,
                        Enumerable.Range(0, HumanFortress.Simulation.World.Chunk.CELLS_PER_LAYER)
                            .Where(shard.ContainsCell)
                            .ToArray()))
                    .ToArray(),
                DirtyGeneration);
        }
    }

    internal void RestoreMutationMemento(MutationMemento memento)
    {
        lock (_readLock)
        {
            _shards.Clear();
            Array.Clear(_cellZones);
            foreach (var state in memento.Shards.OrderBy(static state => state.ZoneId))
            {
                var shard = new ZoneShard(state.ZoneId, state.ChunkKey);
                shard.AddCells(state.Cells);
                _shards.Add(state.ZoneId, shard);
                foreach (var cell in state.Cells)
                    _cellZones[cell] = state.ZoneId;
            }

            DirtyGeneration = memento.DirtyGeneration;
        }
    }
}
