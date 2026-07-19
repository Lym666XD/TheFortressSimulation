using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

internal sealed partial class StockpileDiffLog
{
    private readonly List<StockpileDiff> _ops = new();
    private readonly object _lock = new();
    private int _localSeq;

    internal int PendingCreateZoneCount
    {
        get
        {
            lock (_lock)
            {
                return _ops.Count(static op => op.Op == StockpileDiffOp.CreateZone);
            }
        }
    }

    internal void AddCreateZone(
        string name,
        ChunkKey homeChunk,
        IReadOnlyDictionary<ChunkKey, IReadOnlyList<int>> cellsByChunk,
        ulong createdTick,
        int priority,
        string systemId,
        StockpileFilter? filter = null,
        int zonePriority = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(cellsByChunk);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        var copiedCells = cellsByChunk
            .OrderBy(static entry => entry.Key.Z)
            .ThenBy(static entry => entry.Key.ChunkY)
            .ThenBy(static entry => entry.Key.ChunkX)
            .ToDictionary(
                static entry => entry.Key,
                static entry => (IReadOnlyList<int>)entry.Value
                    .Distinct()
                    .OrderBy(static cell => cell)
                    .ToArray());

        lock (_lock)
        {
            _ops.Add(new StockpileDiff
            {
                Op = StockpileDiffOp.CreateZone,
                TargetChunk = homeChunk,
                ZoneId = 0,
                CellIndex = -1,
                ItemHandle = 0,
                Quantity = copiedCells.Sum(static entry => entry.Value.Count),
                Priority = priority,
                SystemId = systemId,
                LocalSeq = _localSeq++,
                JobId = 0,
                Data = new StockpileCreateZoneData(
                    name,
                    homeChunk,
                    createdTick,
                    copiedCells,
                    filter ?? new StockpileFilter(),
                    zonePriority)
            });
        }
    }

    internal void AddDeleteZone(
        int zoneId,
        int priority,
        string systemId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        lock (_lock)
        {
            _ops.Add(new StockpileDiff
            {
                Op = StockpileDiffOp.DeleteZone,
                TargetChunk = new ChunkKey(0, 0, 0),
                ZoneId = zoneId,
                CellIndex = -1,
                ItemHandle = 0,
                Quantity = 0,
                Priority = priority,
                SystemId = systemId,
                LocalSeq = _localSeq++,
                JobId = 0,
                Data = null
            });
        }
    }

    internal void AddPlaceItem(
        ulong itemHandle,
        ChunkKey chunk,
        int cellIndex,
        int zoneId,
        int quantity,
        int priority,
        string systemId,
        ItemStackRef? stack = null)
    {
        AddItemIndexDiff(
            StockpileDiffOp.PlaceItem,
            itemHandle,
            chunk,
            cellIndex,
            zoneId,
            quantity,
            priority,
            systemId,
            stack);
    }

    internal void AddRemoveItem(
        ulong itemHandle,
        ChunkKey chunk,
        int cellIndex,
        int zoneId,
        int quantity,
        int priority,
        string systemId,
        ItemStackRef? stack = null)
    {
        AddItemIndexDiff(
            StockpileDiffOp.RemoveItem,
            itemHandle,
            chunk,
            cellIndex,
            zoneId,
            quantity,
            priority,
            systemId,
            stack);
    }

    internal void AddReleaseSlot(
        ChunkKey chunk,
        int zoneId,
        int priority,
        string systemId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        lock (_lock)
        {
            _ops.Add(new StockpileDiff
            {
                Op = StockpileDiffOp.ReleaseSlot,
                TargetChunk = chunk,
                ZoneId = zoneId,
                CellIndex = -1,
                ItemHandle = 0,
                Quantity = 0,
                Priority = priority,
                SystemId = systemId,
                LocalSeq = _localSeq++,
                JobId = 0,
                Data = null
            });
        }
    }

    internal void AddReserveSlot(
        ChunkKey chunk,
        int zoneId,
        int priority,
        string systemId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        lock (_lock)
        {
            _ops.Add(new StockpileDiff
            {
                Op = StockpileDiffOp.ReserveSlot,
                TargetChunk = chunk,
                ZoneId = zoneId,
                CellIndex = -1,
                ItemHandle = 0,
                Quantity = 0,
                Priority = priority,
                SystemId = systemId,
                LocalSeq = _localSeq++,
                JobId = 0,
                Data = null
            });
        }
    }

    private void AddItemIndexDiff(
        StockpileDiffOp op,
        ulong itemHandle,
        ChunkKey chunk,
        int cellIndex,
        int zoneId,
        int quantity,
        int priority,
        string systemId,
        ItemStackRef? stack)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoneId);
        ArgumentOutOfRangeException.ThrowIfNegative(cellIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        lock (_lock)
        {
            _ops.Add(new StockpileDiff
            {
                Op = op,
                TargetChunk = chunk,
                ZoneId = zoneId,
                CellIndex = cellIndex,
                ItemHandle = itemHandle,
                Quantity = quantity,
                Priority = priority,
                SystemId = systemId,
                LocalSeq = _localSeq++,
                JobId = 0,
                Data = stack.HasValue ? new StockpileItemIndexData(stack.Value) : null
            });
        }
    }

    internal IReadOnlyList<StockpileDiff> MergeAndSort()
    {
        lock (_lock)
        {
            _ops.Sort(StockpileDiff.CompareDeterministic);
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
