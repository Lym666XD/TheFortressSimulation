using System.Collections.Generic;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Result of collision detection
/// </summary>
internal sealed class CollisionResult
{
    internal bool CanPlace { get; set; }
    internal string? FailureReason { get; set; }
    internal List<Point> BlockedCells { get; set; } = new();
}
