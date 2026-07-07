using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

internal sealed class OrderDiffLog
{
    private readonly List<OrderDiff> _ops = new();
    private readonly object _lock = new();
    private int _localSeq;

    internal void AddMining(Rectangle worldRect, int z, int priority, ulong createdTick, string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new OrderDiff
        {
            Op = OrderDiffOp.Mining,
            WorldRect = worldRect,
            Anchor = new Point(0, 0),
            Z = z,
            ZMin = z,
            ZMax = z,
            MiningAction = MiningAction.Dig,
            ConstructionShape = ConstructionShape.Floor,
            MaterialFilter = null,
            ConstructionId = string.Empty,
            Priority = priority,
            CreatedTick = createdTick,
            SystemId = systemId
        });
    }

    internal void AddAdvancedMining(
        Rectangle worldRect,
        int zMin,
        int zMax,
        MiningAction action,
        int priority,
        ulong createdTick,
        string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new OrderDiff
        {
            Op = OrderDiffOp.AdvancedMining,
            WorldRect = worldRect,
            Anchor = new Point(0, 0),
            Z = zMin,
            ZMin = zMin,
            ZMax = zMax,
            MiningAction = action,
            ConstructionShape = ConstructionShape.Floor,
            MaterialFilter = null,
            ConstructionId = string.Empty,
            Priority = priority,
            CreatedTick = createdTick,
            SystemId = systemId
        });
    }

    internal void AddHaul(Rectangle worldRect, int z, int priority, ulong createdTick, string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new OrderDiff
        {
            Op = OrderDiffOp.Haul,
            WorldRect = worldRect,
            Anchor = new Point(0, 0),
            Z = z,
            ZMin = z,
            ZMax = z,
            MiningAction = MiningAction.Dig,
            ConstructionShape = ConstructionShape.Floor,
            MaterialFilter = null,
            ConstructionId = string.Empty,
            Priority = priority,
            CreatedTick = createdTick,
            SystemId = systemId
        });
    }

    internal void AddConstruction(
        Rectangle worldRect,
        int zMin,
        int zMax,
        ConstructionShape shape,
        MaterialFilterSpec filter,
        int priority,
        ulong createdTick,
        string systemId)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new OrderDiff
        {
            Op = OrderDiffOp.Construction,
            WorldRect = worldRect,
            Anchor = new Point(0, 0),
            Z = zMin,
            ZMin = zMin,
            ZMax = zMax,
            MiningAction = MiningAction.Dig,
            ConstructionShape = shape,
            MaterialFilter = filter,
            ConstructionId = string.Empty,
            Priority = priority,
            CreatedTick = createdTick,
            SystemId = systemId
        });
    }

    internal void AddBuildableConstruction(
        string constructionId,
        Point anchor,
        int z,
        int priority,
        ulong createdTick,
        string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(constructionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new OrderDiff
        {
            Op = OrderDiffOp.BuildableConstruction,
            WorldRect = new Rectangle(anchor.X, anchor.Y, 1, 1),
            Anchor = anchor,
            Z = z,
            ZMin = z,
            ZMax = z,
            MiningAction = MiningAction.Dig,
            ConstructionShape = ConstructionShape.Floor,
            MaterialFilter = null,
            ConstructionId = constructionId,
            Priority = priority,
            CreatedTick = createdTick,
            SystemId = systemId
        });
    }

    internal IReadOnlyList<OrderDiff> MergeAndSort()
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

    private void Add(OrderDiff diff)
    {
        lock (_lock)
        {
            _ops.Add(diff with { LocalSeq = _localSeq++ });
        }
    }
}
