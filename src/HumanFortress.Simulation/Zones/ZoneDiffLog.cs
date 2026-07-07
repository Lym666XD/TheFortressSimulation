using SadRogue.Primitives;

namespace HumanFortress.Simulation.Zones;

internal sealed class ZoneDiffLog
{
    private readonly List<ZoneDiff> _ops = new();
    private readonly object _lock = new();
    private int _localSeq;

    internal void AddCreateZone(
        string defId,
        string name,
        Rectangle worldRect,
        int z,
        ulong createdTick,
        int priority,
        string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new ZoneDiff
        {
            Op = ZoneDiffOp.CreateZone,
            DefId = defId,
            Name = name,
            WorldRect = worldRect,
            Z = z,
            ZoneId = 0,
            CreatedTick = createdTick,
            Priority = priority,
            SystemId = systemId
        });
    }

    internal void AddCells(int zoneId, Rectangle worldRect, int z, int priority, string systemId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new ZoneDiff
        {
            Op = ZoneDiffOp.AddCells,
            DefId = string.Empty,
            Name = string.Empty,
            WorldRect = worldRect,
            Z = z,
            ZoneId = zoneId,
            CreatedTick = 0,
            Priority = priority,
            SystemId = systemId
        });
    }

    internal void RemoveCells(int zoneId, Rectangle worldRect, int z, int priority, string systemId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new ZoneDiff
        {
            Op = ZoneDiffOp.RemoveCells,
            DefId = string.Empty,
            Name = string.Empty,
            WorldRect = worldRect,
            Z = z,
            ZoneId = zoneId,
            CreatedTick = 0,
            Priority = priority,
            SystemId = systemId
        });
    }

    internal void AddDeleteZone(int zoneId, int priority, string systemId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new ZoneDiff
        {
            Op = ZoneDiffOp.DeleteZone,
            DefId = string.Empty,
            Name = string.Empty,
            WorldRect = new Rectangle(0, 0, 0, 0),
            Z = 0,
            ZoneId = zoneId,
            CreatedTick = 0,
            Priority = priority,
            SystemId = systemId
        });
    }

    internal IReadOnlyList<ZoneDiff> MergeAndSort()
    {
        lock (_lock)
        {
            _ops.Sort((a, b) => a.GetSortKey().CompareTo(b.GetSortKey()));
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

    private void Add(ZoneDiff diff)
    {
        lock (_lock)
        {
            _ops.Add(diff with { LocalSeq = _localSeq++ });
        }
    }
}
