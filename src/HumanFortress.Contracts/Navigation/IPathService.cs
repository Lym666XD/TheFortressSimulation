namespace HumanFortress.Contracts.Navigation;

/// <summary>
/// Pathfinding service interface.
/// </summary>
public interface IPathService
{
    /// <summary>
    /// Solve a path request.
    /// </summary>
    Path Solve(in PathRequest request, in IWorldNavigationView world);

    /// <summary>
    /// Start a new tick.
    /// </summary>
    void BeginTick();

    /// <summary>
    /// Invalidate cache for a chunk.
    /// </summary>
    void InvalidateChunk(ChunkKey chunk);
}
