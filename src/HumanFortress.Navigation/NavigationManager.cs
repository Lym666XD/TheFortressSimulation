using System.Collections.Concurrent;

namespace HumanFortress.Navigation;

/// <summary>
/// Manages navigation data for all chunks.
/// Creates and updates ChunkNavData during RebuildDerived phase.
/// </summary>
public sealed class NavigationManager
{
    private readonly ConcurrentDictionary<ChunkKey, ChunkNavData> _navData;
    private readonly HumanFortress.Simulation.World.World _world;
    private readonly NavigationTuning _tuning;

    public NavigationManager(HumanFortress.Simulation.World.World world)
    {
        _world = world;
        _navData = new ConcurrentDictionary<ChunkKey, ChunkNavData>();
        _tuning = new NavigationTuning();
    }

    /// <summary>
    /// Get or create navigation data for a chunk.
    /// </summary>
    public ChunkNavData GetOrCreateNavData(ChunkKey key)
    {
        return _navData.GetOrAdd(key, k => new ChunkNavData(k));
    }

    /// <summary>
    /// Get navigation data for a chunk if it exists.
    /// </summary>
    public ChunkNavData? GetNavData(ChunkKey key)
    {
        return _navData.GetValueOrDefault(key);
    }

    /// <summary>
    /// Get navigation data for a chunk if it exists (using Simulation ChunkKey).
    /// </summary>
    public ChunkNavData? GetNavData(HumanFortress.Simulation.World.ChunkKey simKey)
    {
        var navKey = new ChunkKey(simKey.ChunkX, simKey.ChunkY, simKey.Z);
        return GetNavData(navKey);
    }

    /// <summary>
    /// Get navigation data at world coordinates.
    /// </summary>
    public ChunkNavData? GetNavDataAt(int worldX, int worldY, int z)
    {
        var chunkX = worldX / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        var chunkY = worldY / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        return GetNavData(new ChunkKey(chunkX, chunkY, z));
    }

    /// <summary>
    /// Rebuild navigation data for a chunk (during RebuildDerived phase).
    /// </summary>
    public void RebuildChunkNavData(HumanFortress.Simulation.World.Chunk chunk)
    {
        var navKey = new ChunkKey(chunk.Key.ChunkX, chunk.Key.ChunkY, chunk.Key.Z);
        var navData = GetOrCreateNavData(navKey);
        var tiles = chunk.GetTilesCopy();
        navData.RebuildFromTiles(tiles, _tuning);
    }

    /// <summary>
    /// Rebuild all navigation data.
    /// </summary>
    public void RebuildAll()
    {
        foreach (var chunk in _world.GetAllChunks())
        {
            RebuildChunkNavData(chunk);
        }
    }
}