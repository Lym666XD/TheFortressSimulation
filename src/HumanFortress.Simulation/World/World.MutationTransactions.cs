using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Zones;

namespace HumanFortress.Simulation.World;

internal sealed partial class World
{
    private readonly object _mutationCommitLock = new();

    internal object MutationCommitLock => _mutationCommitLock;

    internal readonly record struct ChunkMutationMemento(
        ChunkKey Key,
        Chunk.MutationMemento State);

    internal readonly record struct MutationMemento(
        IReadOnlyList<ChunkMutationMemento> Chunks,
        IReadOnlyList<ChunkKey> DirtyChunks,
        ItemManager.MutationMemento Items,
        CreatureManager.MutationMemento Creatures,
        OrdersManager.MutationMemento Orders,
        ZoneManager.MutationMemento Zones,
        StockpileManager.MutationMemento Stockpiles);

    internal MutationMemento CaptureMutationMemento()
    {
        lock (_mutationCommitLock)
        {
            ChunkMutationMemento[] chunks;
            lock (_chunkLock)
            {
                chunks = OrderChunks(_chunks.Values)
                    .Select(static chunk => new ChunkMutationMemento(
                        chunk.Key,
                        chunk.CaptureMutationMemento()))
                    .ToArray();
            }

            ChunkKey[] dirtyChunks;
            lock (_dirtyLock)
                dirtyChunks = OrderChunkKeys(_dirtyChunks).ToArray();

            return new MutationMemento(
                chunks,
                dirtyChunks,
                Items.CaptureMutationMemento(),
                Creatures.CaptureMutationMemento(),
                Orders.CaptureMutationMemento(),
                Zones.Manager.CaptureMutationMemento(),
                Stockpiles.CaptureMutationMemento());
        }
    }

    internal void RestoreMutationMemento(MutationMemento memento)
    {
        lock (_mutationCommitLock)
        {
            lock (_chunkLock)
            {
                var retained = memento.Chunks.Select(static entry => entry.Key).ToHashSet();
                foreach (var addedKey in _chunks.Keys.Where(key => !retained.Contains(key)).ToArray())
                    _chunks.Remove(addedKey);

                foreach (var chunkState in memento.Chunks)
                {
                    if (!_chunks.TryGetValue(chunkState.Key, out var chunk))
                    {
                        chunk = new Chunk(chunkState.Key);
                        _chunks.Add(chunkState.Key, chunk);
                    }

                    chunk.RestoreMutationMemento(chunkState.State);
                }
            }

            Items.RestoreMutationMemento(memento.Items);
            Creatures.RestoreMutationMemento(memento.Creatures);
            Orders.RestoreMutationMemento(memento.Orders);
            Zones.Manager.RestoreMutationMemento(memento.Zones);
            Stockpiles.RestoreMutationMemento(memento.Stockpiles);

            lock (_dirtyLock)
            {
                _dirtyChunks.Clear();
                foreach (var key in memento.DirtyChunks)
                    _dirtyChunks.Add(key);
            }
        }
    }
}
