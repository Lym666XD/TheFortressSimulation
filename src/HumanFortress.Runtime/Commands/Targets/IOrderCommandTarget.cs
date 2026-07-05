using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal interface IOrderCommandTarget
{
    void EnqueueMiningOrder(Rectangle worldRect, int z, int priority, ulong createdTick);

    void EnqueueAdvancedMiningOrder(Rectangle worldRect, int zMin, int zMax, MiningAction action, int priority, ulong createdTick);

    void EnqueueHaulOrder(Rectangle worldRect, int z, int priority, ulong createdTick);

    void EnqueueConstructionOrder(Rectangle worldRect, int zMin, int zMax, ConstructionShape shape, MaterialFilterSpec filter, int priority, ulong createdTick);

    void EnqueueBuildableConstructionOrder(string constructionId, Point anchor, int z, int priority, ulong createdTick);
}
