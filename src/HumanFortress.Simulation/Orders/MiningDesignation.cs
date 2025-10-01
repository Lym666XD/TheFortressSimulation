using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// A mining designation created by player input (rectangle at a given Z).
/// Read in the read phase by the mining system to generate planned digs.
/// Thread-safe lifetime: created from UI via command; consumed by MiningSystem.
/// </summary>
public sealed class MiningDesignation
{
    public readonly Rectangle WorldRect;
    public readonly int Z;
    public readonly int Priority; // 0..100
    public readonly ulong CreatedTick;

    public MiningDesignation(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        WorldRect = worldRect;
        Z = z;
        Priority = priority;
        CreatedTick = createdTick;
    }
}

