using System.Collections.Concurrent;

namespace HumanFortress.Navigation;

/// <summary>
/// LRU cache for computed paths.
/// Thread-safe for concurrent reads.
/// </summary>
internal sealed class PathCache
{
    private readonly int _maxSize;
    private readonly ConcurrentDictionary<ulong, CacheEntry> _cache;
    private readonly ConcurrentDictionary<ChunkKey, HashSet<ulong>> _chunkIndex;
    private readonly object _lockObj = new();
    private long _hits;
    private long _misses;
    private ulong _timestamp;

    public PathCache(int maxSize)
    {
        _maxSize = maxSize;
        _cache = new ConcurrentDictionary<ulong, CacheEntry>();
        _chunkIndex = new ConcurrentDictionary<ChunkKey, HashSet<ulong>>();
    }

    public int Count => _cache.Count;
    public long Hits => _hits;
    public long Misses => _misses;

    public bool TryGet(ulong key, out Path path)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            // Update access time
            lock (_lockObj)
            {
                entry.LastAccess = ++_timestamp;
            }

            Interlocked.Increment(ref _hits);
            path = entry.Path;
            return true;
        }

        Interlocked.Increment(ref _misses);
        path = default;
        return false;
    }

    public void Add(ulong key, Path path)
    {
        lock (_lockObj)
        {
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

            if (_cache.TryAdd(key, entry))
            {
                // Index by chunks
                IndexPath(key, path);
            }
        }
    }

    public void InvalidateChunk(ChunkKey chunk)
    {
        lock (_lockObj)
        {
            if (_chunkIndex.TryRemove(chunk, out var keys))
            {
                foreach (var key in keys)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }
    }

    public void Clear()
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

        foreach (var kvp in _cache)
        {
            if (kvp.Value.LastAccess < oldestTime)
            {
                oldestTime = kvp.Value.LastAccess;
                oldestKey = kvp.Key;
            }
        }

        if (oldestKey != 0)
        {
            _cache.TryRemove(oldestKey, out _);
            // Also remove from chunk index
            RemoveFromIndex(oldestKey);
        }
    }

    private void IndexPath(ulong key, Path path)
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
        _chunkIndex.AddOrUpdate(chunk,
            _ => new HashSet<ulong> { key },
            (_, set) =>
            {
                set.Add(key);
                return set;
            });
    }

    private void RemoveFromIndex(ulong key)
    {
        foreach (var kvp in _chunkIndex)
        {
            kvp.Value.Remove(key);
            if (kvp.Value.Count == 0)
            {
                _chunkIndex.TryRemove(kvp.Key, out _);
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
        public Path Path { get; set; }
        public ulong LastAccess { get; set; }
    }
}