using HumanFortress.Contracts.Navigation;
using NavPath = HumanFortress.Contracts.Navigation.Path;

namespace HumanFortress.Navigation.Implementation;

/// <summary>
/// LRU cache for computed paths.
/// Lock-owned cache state; pathfinding may query it from multiple runtime services,
/// but cache eviction and chunk indexes must remain deterministic.
/// </summary>
internal sealed class PathCache
{
    private readonly int _maxSize;
    private readonly Dictionary<ulong, CacheEntry> _cache;
    private readonly Dictionary<ChunkKey, HashSet<ulong>> _chunkIndex;
    private readonly object _lockObj = new();
    private long _hits;
    private long _misses;
    private ulong _timestamp;

    internal PathCache(int maxSize)
    {
        _maxSize = maxSize;
        _cache = new Dictionary<ulong, CacheEntry>();
        _chunkIndex = new Dictionary<ChunkKey, HashSet<ulong>>();
    }

    internal int Count
    {
        get
        {
            lock (_lockObj)
            {
                return _cache.Count;
            }
        }
    }

    internal long Hits
    {
        get
        {
            lock (_lockObj)
            {
                return _hits;
            }
        }
    }

    internal long Misses
    {
        get
        {
            lock (_lockObj)
            {
                return _misses;
            }
        }
    }

    internal bool TryGet(ulong key, out NavPath path)
    {
        lock (_lockObj)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                entry.LastAccess = ++_timestamp;
                _hits++;
                path = entry.Path;
                return true;
            }

            _misses++;
            path = default;
            return false;
        }
    }

    internal void Add(ulong key, NavPath path)
    {
        lock (_lockObj)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                existing.Path = path;
                existing.LastAccess = ++_timestamp;
                RemoveFromIndex(key);
                IndexPath(key, path);
                return;
            }

            // Check if we need to evict
            if (_cache.Count >= _maxSize)
            {
                EvictLRU();
            }

            var entry = new CacheEntry
            {
                Path = path,
                LastAccess = ++_timestamp,
            };

            _cache.Add(key, entry);
            IndexPath(key, path);
        }
    }

    internal void InvalidateChunk(ChunkKey chunk)
    {
        lock (_lockObj)
        {
            if (_chunkIndex.Remove(chunk, out var keys))
            {
                foreach (var key in keys)
                {
                    _cache.Remove(key);
                    RemoveFromIndex(key);
                }
            }
        }
    }

    internal void Clear()
    {
        lock (_lockObj)
        {
            _cache.Clear();
            _chunkIndex.Clear();
            _hits = 0;
            _misses = 0;
            _timestamp = 0;
        }
    }

    private void EvictLRU()
    {
        // Find least recently used
        ulong oldestKey = 0;
        ulong oldestTime = ulong.MaxValue;

        foreach (var kvp in _cache.OrderBy(static kvp => kvp.Key))
        {
            if (kvp.Value.LastAccess < oldestTime)
            {
                oldestTime = kvp.Value.LastAccess;
                oldestKey = kvp.Key;
            }
        }

        if (oldestKey != 0)
        {
            _cache.Remove(oldestKey);
            // Also remove from chunk index
            RemoveFromIndex(oldestKey);
        }
    }

    private void IndexPath(ulong key, NavPath path)
    {
        if (path.Steps.Length == 0)
            return;

        // Index by source and destination chunks
        var srcNode = path.Steps.Span[0];
        var dstNode = path.Steps.Span[path.Steps.Length - 1];

        var srcChunk = ToChunkKey(srcNode.Position);
        var dstChunk = ToChunkKey(dstNode.Position);

        AddToChunkIndex(srcChunk, key);
        if (srcChunk != dstChunk)
        {
            AddToChunkIndex(dstChunk, key);
        }

        // Also index any intermediate chunks
        ChunkKey? prevChunk = null;
        foreach (var node in path.Steps.Span)
        {
            var chunk = ToChunkKey(node.Position);
            if (chunk != prevChunk && chunk != srcChunk && chunk != dstChunk)
            {
                AddToChunkIndex(chunk, key);
            }
            prevChunk = chunk;
        }
    }

    private void AddToChunkIndex(ChunkKey chunk, ulong key)
    {
        if (!_chunkIndex.TryGetValue(chunk, out var keys))
        {
            keys = new HashSet<ulong>();
            _chunkIndex.Add(chunk, keys);
        }

        keys.Add(key);
    }

    private void RemoveFromIndex(ulong key)
    {
        foreach (var kvp in _chunkIndex
                     .OrderBy(static kvp => kvp.Key.Z)
                     .ThenBy(static kvp => kvp.Key.ChunkY)
                     .ThenBy(static kvp => kvp.Key.ChunkX)
                     .ToArray())
        {
            kvp.Value.Remove(key);
            if (kvp.Value.Count == 0)
            {
                _chunkIndex.Remove(kvp.Key);
            }
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

    private class CacheEntry
    {
        internal NavPath Path { get; set; }
        internal ulong LastAccess { get; set; }
    }
}
