using System;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Designation for building an L2 placeable construction (e.g., workshop) at an anchor cell.
/// Planned by BuildableConstructionSystem which places a construction site placeable.
/// </summary>
public sealed class BuildableConstructionDesignation
{
    public readonly string ConstructionId;
    public readonly Point Anchor;
    public readonly int Z;
    public readonly int Priority;
    public readonly ulong CreatedTick;

    public BuildableConstructionDesignation(string constructionId, Point anchor, int z, int priority, ulong createdTick)
    {
        ConstructionId = constructionId;
        Anchor = anchor;
        Z = z;
        Priority = priority;
        CreatedTick = createdTick;
    }
}

