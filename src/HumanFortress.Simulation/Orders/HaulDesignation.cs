using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// A haul designation created by player input (rectangle at a given Z).
/// Read in the read phase by the hauling system to generate moves.
/// Thread-safe lifetime: created from UI via command; consumed by HaulingSystem.
/// </summary>
internal sealed class HaulDesignation
{
    public readonly Rectangle WorldRect;
    public readonly int Z;
    public readonly int Priority; // 0..100
    public readonly ulong CreatedTick;

    public HaulDesignation(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        WorldRect = worldRect;
        Z = z;
        Priority = priority;
        CreatedTick = createdTick;
    }
}

