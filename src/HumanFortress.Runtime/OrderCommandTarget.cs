using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime;

public sealed class OrderCommandTarget : IOrderCommandTarget
{
    private readonly World _world;

    public OrderCommandTarget(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void EnqueueMiningOrder(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        _world.Orders.EnqueueMining(worldRect, z, priority, createdTick);
    }

    public void EnqueueAdvancedMiningOrder(Rectangle worldRect, int zMin, int zMax, MiningAction action, int priority, ulong createdTick)
    {
        _world.Orders.EnqueueMiningAdvanced(worldRect, zMin, zMax, action, priority, createdTick);
    }

    public void EnqueueHaulOrder(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        _world.Orders.EnqueueHaul(worldRect, z, priority, createdTick);
    }

    public void EnqueueConstructionOrder(Rectangle worldRect, int zMin, int zMax, ConstructionShape shape, MaterialFilterSpec filter, int priority, ulong createdTick)
    {
        _world.Orders.EnqueueConstruction(worldRect, zMin, zMax, shape, filter, priority, createdTick);
    }

    public void EnqueueBuildableConstructionOrder(string constructionId, Point anchor, int z, int priority, ulong createdTick)
    {
        _world.Orders.EnqueueBuildableConstruction(constructionId, anchor, z, priority, createdTick);
    }
}
