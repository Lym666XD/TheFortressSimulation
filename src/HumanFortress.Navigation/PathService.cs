using HumanFortress.Contracts.Navigation;
using NavPath = HumanFortress.Contracts.Navigation.Path;

namespace HumanFortress.Navigation.Implementation;

/// <summary>
/// Pathfinding service per NAVIGATION_SPEC.md section 5.
/// Manages path requests, caching, and deterministic pathfinders.
/// </summary>
internal sealed class PathService : IPathService
{
    private readonly NavigationTuning _tuning;
    private readonly PathCache _cache;
    private readonly ThreadLocal<DeterministicAStar> _pathfinders;
    private int _pathRequestsServedThisTick;

    internal PathService(NavigationTuning? tuning = null)
    {
        _tuning = tuning ?? NavigationTuning.Default;
        _cache = new PathCache(1024); // LRU cache with 1024 entries
        _pathfinders = new ThreadLocal<DeterministicAStar>(() => new DeterministicAStar(_tuning));
    }

    /// <summary>
    /// Solve a path request.
    /// Called during read phase of UPDATE_ORDER.
    /// </summary>
    internal NavPath Solve(in PathRequest request, in IWorldNavigationView world)
    {
        // Check deterministic per-tick request budget before cache lookup.
        // Cache state must not let warm-cache sessions serve more simulation-
        // visible path requests than cold-cache sessions.
        if (_pathRequestsServedThisTick >= _tuning.MaxPathsPerTick)
        {
            return NavPath.BudgetExhausted;
        }

        _pathRequestsServedThisTick++;

        // Check cache after consuming request budget.
        var cacheKey = GenerateCacheKey(request, world);
        if (_cache.TryGet(cacheKey, out var cachedPath))
        {
            return cachedPath;
        }

        // Compute path
        var pathfinder = _pathfinders.Value!;
        int nodeBudget = CalculateNodeBudget(_tuning.MaxNodesPerSearch, request.EffectiveSearchAttempt);
        var path = pathfinder.FindPath(request, world, nodeBudget);

        // Only a terminal path to the requested destination can enter the complete cache.
        if (path.ReachesDestination(request.Destination))
        {
            _cache.Add(cacheKey, path);
        }
        return path;
    }

    /// <summary>
    /// Start a new tick and reset the deterministic request counter.
    /// </summary>
    internal void BeginTick()
    {
        _pathRequestsServedThisTick = 0;
    }

    /// <summary>
    /// Invalidate cache entries for a specific chunk.
    /// Called when chunk connectivity version changes.
    /// </summary>
    internal void InvalidateChunk(ChunkKey chunk)
    {
        _cache.InvalidateChunk(chunk);
    }

    /// <summary>
    /// Clear all cached paths.
    /// </summary>
    internal void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Get statistics for debugging.
    /// </summary>
    internal PathServiceStats GetStats()
    {
        return new PathServiceStats
        {
            CacheSize = _cache.Count,
            CacheHits = _cache.Hits,
            CacheMisses = _cache.Misses,
            PathsComputedThisTick = _pathRequestsServedThisTick,
        };
    }

    private ulong GenerateCacheKey(PathRequest request, IWorldNavigationView world)
    {
        // Include connectivity versions in cache key
        var srcChunk = ToChunkKey(request.Source);
        var dstChunk = ToChunkKey(request.Destination);
        var srcVersion = world.GetConnectivityVersion(srcChunk);
        var dstVersion = world.GetConnectivityVersion(dstChunk);

        unchecked
        {
            ulong key = request.GetCacheKey();
            key = key * 31 + (ulong)srcVersion;
            key = key * 31 + (ulong)dstVersion;
            return key;
        }
    }

    private static ChunkKey ToChunkKey(Point3 position)
    {
        const int ChunkSize = 32;
        return new ChunkKey(
            position.X / ChunkSize,
            position.Y / ChunkSize,
            position.Z);
    }

    private static int CalculateNodeBudget(int baseBudget, byte searchAttempt)
    {
        int shift = Math.Min(searchAttempt, PathRequest.MaxSearchAttempt);
        long scaled = (long)Math.Max(1, baseBudget) << shift;
        return scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
    }

    internal void Dispose()
    {
        _pathfinders?.Dispose();
    }

    NavPath IPathService.Solve(in PathRequest request, in IWorldNavigationView world) => Solve(in request, in world);

    void IPathService.BeginTick() => BeginTick();

    void IPathService.InvalidateChunk(ChunkKey chunk) => InvalidateChunk(chunk);
}

/// <summary>
/// Statistics for pathfinding service.
/// </summary>
internal struct PathServiceStats
{
    internal int CacheSize;
    internal long CacheHits;
    internal long CacheMisses;
    internal int PathsComputedThisTick;
}
