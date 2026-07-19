using HumanFortress.Contracts.Navigation;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Diagnostics;

namespace HumanFortress.Runtime.Navigation;

/// <summary>
/// Runtime-owned registry for active path services that cache navigation paths.
/// Navigation owns nav data; Runtime owns broadcasting cache invalidation after
/// simulation mutations rebuild dirty navigation chunks.
/// </summary>
internal sealed class RuntimePathServiceRegistry
{
    private readonly object _lock = new();
    private readonly List<IPathService> _pathServices = new();

    internal void Register(IPathService pathService)
    {
        ArgumentNullException.ThrowIfNull(pathService);

        lock (_lock)
        {
            if (!_pathServices.Contains(pathService))
                _pathServices.Add(pathService);
        }
    }

    internal void Clear()
    {
        lock (_lock)
        {
            _pathServices.Clear();
        }
    }

    internal void InvalidateChunk(ChunkKey chunk)
    {
        IPathService[] snapshot;
        lock (_lock)
        {
            snapshot = _pathServices.ToArray();
        }

        foreach (var pathService in snapshot)
            pathService.InvalidateChunk(chunk);
    }

    internal RuntimePathMetricsSnapshot CaptureMetrics()
    {
        IPathService[] snapshot;
        lock (_lock)
        {
            snapshot = _pathServices.ToArray();
        }

        long requests = 0;
        long hits = 0;
        long misses = 0;
        long entries = 0;
        var known = 0;
        foreach (var service in snapshot)
        {
            if (service is not PathService pathService)
                continue;

            var stats = pathService.GetStats();
            known++;
            requests += stats.PathsComputedThisTick;
            hits += stats.CacheHits;
            misses += stats.CacheMisses;
            entries += stats.CacheSize;
        }

        return new RuntimePathMetricsSnapshot(
            InstrumentationIsComplete: known == snapshot.Length,
            RegisteredServiceCountCurrent: snapshot.Length,
            InstrumentedServiceCountCurrent: known,
            RequestsServedThisTick: requests,
            CacheHitsTotal: hits,
            CacheMissesTotal: misses,
            CacheEntriesCurrent: entries);
    }
}
