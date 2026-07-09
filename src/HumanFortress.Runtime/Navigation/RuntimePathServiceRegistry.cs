using HumanFortress.Contracts.Navigation;

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
}
