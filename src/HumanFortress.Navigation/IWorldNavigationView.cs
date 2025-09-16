namespace HumanFortress.Navigation;

/// <summary>
/// Read-only view of world navigation data for pathfinding.
/// Provides access to navigation masks and costs across chunks.
/// </summary>
public interface IWorldNavigationView
{
    /// <summary>
    /// Check if a position is valid in the world.
    /// </summary>
    bool IsValid(Point3 position);

    /// <summary>
    /// Get navigation capabilities at a position.
    /// </summary>
    NavCapability GetCapabilities(Point3 position);

    /// <summary>
    /// Get movement cost at a position.
    /// </summary>
    ushort GetCost(Point3 position);

    /// <summary>
    /// Check if a position is walkable with given mode.
    /// </summary>
    bool IsWalkable(Point3 position, MoveMode mode);

    /// <summary>
    /// Check if position has stairs up.
    /// </summary>
    bool HasStairsUp(Point3 position);

    /// <summary>
    /// Check if position has stairs down.
    /// </summary>
    bool HasStairsDown(Point3 position);

    /// <summary>
    /// Get connectivity version for a chunk.
    /// </summary>
    int GetConnectivityVersion(ChunkKey chunk);
}