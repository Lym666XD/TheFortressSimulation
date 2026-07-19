using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.Zones;

namespace HumanFortress.Simulation.World;

internal sealed partial class Chunk
{
    internal readonly record struct WorkshopMemento(
        PlaceableInstance Placeable,
        WorkshopState? Workshop);

    internal readonly record struct MutationMemento(
        TileBase[] Tiles,
        int LodLevel,
        ulong LastModifiedTick,
        ulong ConnectivityVersion,
        IReadOnlyList<int> DirtyTiles,
        ChunkZoneData.MutationMemento? Zones,
        ChunkStockpileData.MutationMemento? Stockpiles,
        IReadOnlyList<WorkshopMemento> Workshops);

    internal MutationMemento CaptureMutationMemento()
    {
        lock (_writeLock)
        {
            var workshops = _placeableData?.GetOwnedPlaceableSnapshot()
                .OrderBy(static entry => entry.Placeable.Guid)
                .Select(static entry => new WorkshopMemento(
                    entry.Placeable,
                    entry.Placeable.Workshop?.CloneForMutationMemento()))
                .ToArray()
                ?? Array.Empty<WorkshopMemento>();
            return new MutationMemento(
                (TileBase[])_tiles.Clone(),
                LODLevel,
                LastModifiedTick,
                ConnectivityVersion,
                _dirtyTiles.ToArray(),
                _zoneData?.CaptureMutationMemento(),
                _stockpileData?.CaptureMutationMemento(),
                workshops);
        }
    }

    internal void RestoreMutationMemento(MutationMemento memento)
    {
        lock (_writeLock)
        {
            if (memento.Tiles.Length != CELLS_PER_LAYER)
                throw new InvalidOperationException("Chunk mutation memento has an invalid tile count.");

            Array.Copy(memento.Tiles, _tiles, CELLS_PER_LAYER);
            LODLevel = memento.LodLevel;
            LastModifiedTick = memento.LastModifiedTick;
            ConnectivityVersion = memento.ConnectivityVersion;
            _dirtyTiles.Clear();
            foreach (var localIndex in memento.DirtyTiles)
                _dirtyTiles.Add(localIndex);

            if (memento.Zones.HasValue)
            {
                _zoneData ??= new ChunkZoneData();
                _zoneData.RestoreMutationMemento(memento.Zones.Value);
            }
            else
            {
                _zoneData = null;
            }

            if (memento.Stockpiles.HasValue)
            {
                _stockpileData ??= new ChunkStockpileData();
                _stockpileData.RestoreMutationMemento(memento.Stockpiles.Value);
            }
            else
            {
                _stockpileData = null;
            }

            foreach (var workshop in memento.Workshops)
                workshop.Placeable.Workshop = workshop.Workshop?.CloneForMutationMemento();
        }
    }
}
