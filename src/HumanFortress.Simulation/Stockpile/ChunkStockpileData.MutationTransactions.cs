namespace HumanFortress.Simulation.Stockpile;

internal sealed partial class ChunkStockpileData
{
    internal readonly record struct MutationMemento(
        IReadOnlyList<ZoneShard.MutationMemento> Shards,
        IReadOnlyDictionary<string, IReadOnlyList<ulong>> ItemsByTag,
        IReadOnlyDictionary<int, IReadOnlyList<ulong>> ItemsByZone,
        IReadOnlyList<ulong> LooseItems,
        uint DirtyGeneration);

    internal MutationMemento CaptureMutationMemento()
    {
        lock (_readLock)
        {
            return new MutationMemento(
                _shards.Values
                    .OrderBy(static shard => shard.ZoneId)
                    .Select(static shard => shard.CaptureMutationMemento())
                    .ToArray(),
                _itemsByTag
                    .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                    .ToDictionary(
                        static entry => entry.Key,
                        static entry => (IReadOnlyList<ulong>)entry.Value.Order().ToArray(),
                        StringComparer.Ordinal),
                _itemsByZone
                    .OrderBy(static entry => entry.Key)
                    .ToDictionary(
                        static entry => entry.Key,
                        static entry => (IReadOnlyList<ulong>)entry.Value.Order().ToArray()),
                _looseItems.Order().ToArray(),
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
                var shard = ZoneShard.RestoreMutationMemento(state);
                _shards.Add(shard.ZoneId, shard);
                foreach (var cell in state.Cells)
                    _cellZones[cell] = state.ZoneId;
            }

            _itemsByTag.Clear();
            foreach (var entry in memento.ItemsByTag.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
                _itemsByTag.Add(entry.Key, entry.Value.Order().ToList());

            _itemsByZone.Clear();
            foreach (var entry in memento.ItemsByZone.OrderBy(static entry => entry.Key))
                _itemsByZone.Add(entry.Key, entry.Value.Order().ToList());

            _looseItems.Clear();
            _looseItems.AddRange(memento.LooseItems.Order());
            DirtyGeneration = memento.DirtyGeneration;
        }
    }
}
