using System;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

/// <summary>
/// Designation for building an L2 placeable construction (e.g., workshop) at an anchor cell.
/// Planned by BuildableConstructionSystem which places a construction site placeable.
/// </summary>
internal sealed class BuildableConstructionDesignation
{
    internal readonly string ConstructionId;
    internal readonly Point Anchor;
    internal readonly int Z;
    internal readonly int Priority;
    internal readonly ulong CreatedTick;

    internal BuildableConstructionDesignation(string constructionId, Point anchor, int z, int priority, ulong createdTick)
    {
        ConstructionId = constructionId;
        Anchor = anchor;
        Z = z;
        Priority = priority;
        CreatedTick = createdTick;
    }
}
