using HumanFortress.Contracts.Navigation;
using System.Collections.Generic;
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
    private readonly Queue<PathRequest> _requestQueue;
    private readonly ThreadLocal<DeterministicAStar> _pathfinders;
    private int _pathRequestsServedThisTick;

    internal PathService(NavigationTuning? tuning = null)
    {
        _tuning = tuning ?? NavigationTuning.Default;
        _cache = new PathCache(1024); // LRU cache with 1024 entries
        _requestQueue = new Queue<PathRequest>();
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
            // Queue for next tick
            _requestQueue.Enqueue(request);
            return NavPath.Invalid;
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
        var path = pathfinder.FindPath(request, world);

        // Cache successful paths
        if (path.Kind == PathResultKind.Found)
        {
            _cache.Add(cacheKey, path);
        }
        return path;
    }

    /// <summary>
    /// Start a new tick - resets counters and processes queued requests.
    /// </summary>
    internal void BeginTick()
    {
        _pathRequestsServedThisTick = 0;
    }

    /// <summary>
    /// Process queued path requests up to the deterministic request budget.
    /// </summary>
    internal void ProcessQueuedRequests(IWorldNavigationView world)
    {
        while (_requestQueue.Count > 0 && _pathRequestsServedThisTick < _tuning.MaxPathsPerTick)
        {
            var request = _requestQueue.Dequeue();
            Solve(in request, in world);
        }
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
            QueuedRequests = _requestQueue.Count,
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

    internal void Dispose()
    {
        _pathfinders?.Dispose();
    }

    NavPath IPathService.Solve(in PathRequest request, in IWorldNavigationView world) => Solve(in request, in world);

    void IPathService.BeginTick() => BeginTick();

    void IPathService.ProcessQueuedRequests(IWorldNavigationView world) => ProcessQueuedRequests(world);

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
    internal int QueuedRequests;
    internal int PathsComputedThisTick;
}
