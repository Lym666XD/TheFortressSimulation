using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Runtime;

internal sealed class OrderCommandTarget : IOrderCommandTarget
{
    private const string SystemId = "Runtime.OrderCommand";

    private readonly OrderDiffLog _orderDiffLog;

    internal OrderCommandTarget(OrderDiffLog orderDiffLog)
    {
        _orderDiffLog = orderDiffLog ?? throw new ArgumentNullException(nameof(orderDiffLog));
    }

    void IOrderCommandTarget.EnqueueMiningOrder(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        _orderDiffLog.AddMining(worldRect, z, priority, createdTick, SystemId);
    }

    void IOrderCommandTarget.EnqueueAdvancedMiningOrder(Rectangle worldRect, int zMin, int zMax, MiningAction action, int priority, ulong createdTick)
    {
        _orderDiffLog.AddAdvancedMining(worldRect, zMin, zMax, action, priority, createdTick, SystemId);
    }

    void IOrderCommandTarget.EnqueueHaulOrder(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        _orderDiffLog.AddHaul(worldRect, z, priority, createdTick, SystemId);
    }

    void IOrderCommandTarget.EnqueueConstructionOrder(Rectangle worldRect, int zMin, int zMax, ConstructionShape shape, MaterialFilterSpec filter, int priority, ulong createdTick)
    {
        _orderDiffLog.AddConstruction(worldRect, zMin, zMax, shape, filter, priority, createdTick, SystemId);
    }

    void IOrderCommandTarget.EnqueueBuildableConstructionOrder(string constructionId, Point anchor, int z, int priority, ulong createdTick)
    {
        _orderDiffLog.AddBuildableConstruction(constructionId, anchor, z, priority, createdTick, SystemId);
    }
}
