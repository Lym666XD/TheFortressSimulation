using HumanFortress.Core.Commands;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using System.Collections.Concurrent;

namespace HumanFortress.Simulation.World;

/// <summary>
/// World manager handling chunks and LOD per SIM_LOD_POLICY.md.
/// Fortress size: N×N chunks, N ∈ [2..8]; chunk = 32×32×Z tiles.
/// </summary>
internal sealed class World : IWorldReader
{
    private readonly ConcurrentDictionary<ChunkKey, Chunk> _chunks;
    private readonly int _sizeInChunks;
    private readonly int _maxZ;
    private readonly System.Collections.Generic.HashSet<ChunkKey> _dirtyChunks = new();
    private readonly object _dirtyLock = new();

    // Global managers (singleton per world)
    public CreatureManager Creatures { get; }
    public ItemManager Items { get; }
    public HumanFortress.Simulation.Orders.OrdersManager Orders { get; }
    public HumanFortress.Simulation.Stockpile.StockpileManager Stockpiles { get; }
    public HumanFortress.Simulation.Jobs.ReservationManager Reservations { get; }
    public HumanFortress.Simulation.Zones.ZoneCoordinator Zones { get; }

    public World(int sizeInChunks, int maxZ)
    {
        if (sizeInChunks < 2 || sizeInChunks > 8)
            throw new ArgumentException("Size must be between 2 and 8 chunks", nameof(sizeInChunks));

        _sizeInChunks = sizeInChunks;
        _maxZ = maxZ;
        _chunks = new ConcurrentDictionary<ChunkKey, Chunk>();

        // Initialize managers
        Creatures = new CreatureManager();
        Items = new ItemManager();
        Orders = new HumanFortress.Simulation.Orders.OrdersManager();
        Stockpiles = new HumanFortress.Simulation.Stockpile.StockpileManager();
        Reservations = new HumanFortress.Simulation.Jobs.ReservationManager();

        var zoneManager = new HumanFortress.Simulation.Zones.ZoneManager();
        Zones = new HumanFortress.Simulation.Zones.ZoneCoordinator(this, zoneManager);

        // Set self-reference
        Creatures.SetWorld(this);
    }

    /// <summary>
    /// Size of the world in chunks (N for N×N).
    /// </summary>
    public int SizeInChunks => _sizeInChunks;

    /// <summary>
    /// Size of the world in tiles.
    /// </summary>
    public int SizeInTiles => _sizeInChunks * Chunk.SIZE_XY;

    /// <summary>
    /// Maximum Z level.
    /// </summary>
    public int MaxZ => _maxZ;

    /// <summary>
    /// Get or create a chunk.
    /// </summary>
    public Chunk GetOrCreateChunk(ChunkKey key)
    {
        return _chunks.GetOrAdd(key, k => new Chunk(k));
    }

    /// <summary>
    /// Get chunk if it exists.
    /// </summary>
    public Chunk? GetChunk(ChunkKey key)
    {
        return _chunks.GetValueOrDefault(key);
    }

    /// <summary>
    /// Get tile at world coordinates.
    /// </summary>
    public TileBase? GetTile(int worldX, int worldY, int z)
    {
        if (!IsValidPosition(worldX, worldY, z))
            return null;

        var chunkX = worldX / Chunk.SIZE_XY;
        var chunkY = worldY / Chunk.SIZE_XY;
        var localX = worldX % Chunk.SIZE_XY;
        var localY = worldY % Chunk.SIZE_XY;

        var chunk = GetChunk(new ChunkKey(chunkX, chunkY, z));
        return chunk?.GetTile(localX, localY);
    }

    /// <summary>
    /// Set tile at world coordinates. Must be called only during Write phase.
    /// </summary>
    public void SetTile(int worldX, int worldY, int z, TileBase tile, ulong tick)
    {
        if (!IsValidPosition(worldX, worldY, z))
            throw new ArgumentOutOfRangeException();

        var chunkX = worldX / Chunk.SIZE_XY;
        var chunkY = worldY / Chunk.SIZE_XY;
        var localX = worldX % Chunk.SIZE_XY;
        var localY = worldY % Chunk.SIZE_XY;

        var chunk = GetOrCreateChunk(new ChunkKey(chunkX, chunkY, z));
        chunk.SetTile(localX, localY, tile, tick);
    }

    /// <summary>
    /// Check if position is valid.
    /// </summary>
    public bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < SizeInTiles &&
               y >= 0 && y < SizeInTiles &&
               z >= 0 && z < _maxZ;
    }

    /// <summary>
    /// Get all chunks in the world.
    /// </summary>
    public IEnumerable<Chunk> GetAllChunks()
    {
        return _chunks.Values;
    }

    /// <summary>
    /// Get chunk at chunk coordinates.
    /// </summary>
    public Chunk? GetChunkAt(int chunkX, int chunkY, int z)
    {
        return GetChunk(new ChunkKey(chunkX, chunkY, z));
    }

    /// <summary>
    /// Get all active chunks (L0/L1).
    /// </summary>
    public IEnumerable<Chunk> GetActiveChunks()
    {
        return _chunks.Values.Where(c => c.LODLevel <= 1);
    }

    /// <summary>
    /// Mark chunk as dirty for navigation rebuild.
    /// </summary>
    public void MarkChunkDirty(ChunkKey key)
    {
        lock (_dirtyLock)
        {
            _dirtyChunks.Add(key);
        }
    }

    /// <summary>
    /// Get and clear all dirty chunks for navigation rebuild.
    /// </summary>
    internal List<ChunkKey> GetAndClearDirtyChunks()
    {
        lock (_dirtyLock)
        {
            var list = new List<ChunkKey>(_dirtyChunks);
            _dirtyChunks.Clear();
            return list;
        }
    }

    /// <summary>
    /// Update LOD levels based on player focus per SIM_LOD_POLICY.md.
    /// </summary>
    public void UpdateLOD(int focusX, int focusY, int focusZ)
    {
        var focusChunkX = focusX / Chunk.SIZE_XY;
        var focusChunkY = focusY / Chunk.SIZE_XY;

        foreach (var chunk in _chunks.Values)
        {
            var distX = Math.Abs(chunk.Key.ChunkX - focusChunkX);
            var distY = Math.Abs(chunk.Key.ChunkY - focusChunkY);
            var distZ = Math.Abs(chunk.Key.Z - focusZ);
            var maxDist = Math.Max(Math.Max(distX, distY), distZ);

            // L0: focus chunk
            // L1: adjacent chunks (distance 1)
            // L2: nearby chunks (distance 2-3)
            // L3+: far chunks
            if (maxDist == 0)
                chunk.LODLevel = 0;
            else if (maxDist == 1)
                chunk.LODLevel = 1;
            else if (maxDist <= 3)
                chunk.LODLevel = 2;
            else
                chunk.LODLevel = 3;
        }
    }

    /// <summary>
    /// Get chunks that need edge-band checking for incidents.
    /// </summary>
    public IEnumerable<(ChunkKey key, EdgeBand band)> GetEdgeBandChunks()
    {
        for (int z = 0; z < _maxZ; z++)
        {
            // North edge
            for (int cx = 0; cx < _sizeInChunks; cx++)
            {
                yield return (new ChunkKey(cx, 0, z), EdgeBand.North);
            }

            // South edge
            for (int cx = 0; cx < _sizeInChunks; cx++)
            {
                yield return (new ChunkKey(cx, _sizeInChunks - 1, z), EdgeBand.South);
            }

            // West edge (excluding corners already covered)
            for (int cy = 1; cy < _sizeInChunks - 1; cy++)
            {
                yield return (new ChunkKey(0, cy, z), EdgeBand.West);
            }

            // East edge (excluding corners already covered)
            for (int cy = 1; cy < _sizeInChunks - 1; cy++)
            {
                yield return (new ChunkKey(_sizeInChunks - 1, cy, z), EdgeBand.East);
            }
        }
    }
}

/// <summary>
/// Edge band directions for incident spawning.
/// </summary>
internal enum EdgeBand
{
    North,
    East,
    South,
    West
}
