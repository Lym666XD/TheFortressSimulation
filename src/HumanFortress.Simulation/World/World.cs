using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;

namespace HumanFortress.Simulation.World;

/// <summary>
/// World manager handling chunks and LOD per SIM_LOD_POLICY.md.
/// Fortress size: N×N chunks; chunk = 32×32×Z tiles.
/// </summary>
internal sealed partial class World : IWorldReader
{
    internal const int MinSizeInChunks = FortressSessionSizeLimits.MinFortressSize;
    internal const int MaxSizeInChunks = FortressSessionSizeLimits.MaxFortressSize;

    private readonly Dictionary<ChunkKey, Chunk> _chunks;
    private readonly int _sizeInChunks;
    private readonly int _maxZ;
    private readonly System.Collections.Generic.HashSet<ChunkKey> _dirtyChunks = new();
    private readonly object _chunkLock = new();
    private readonly object _dirtyLock = new();
    private readonly object _topologyLock = new();
    private IDiagnosticSink _diagnostics;

    // Global managers (singleton per world)
    internal CreatureManager Creatures { get; }
    internal ItemManager Items { get; }
    internal HumanFortress.Simulation.Orders.OrdersManager Orders { get; }
    internal HumanFortress.Simulation.Stockpile.StockpileManager Stockpiles { get; }
    internal HumanFortress.Simulation.Jobs.ReservationManager Reservations { get; }
    internal HumanFortress.Simulation.Zones.ZoneCoordinator Zones { get; }

    internal World(int sizeInChunks, int maxZ, IDiagnosticSink? diagnostics = null)
    {
        if (sizeInChunks < MinSizeInChunks || sizeInChunks > MaxSizeInChunks)
            throw new ArgumentException($"Size must be between {MinSizeInChunks} and {MaxSizeInChunks} chunks", nameof(sizeInChunks));

        _sizeInChunks = sizeInChunks;
        _maxZ = maxZ;
        _chunks = new Dictionary<ChunkKey, Chunk>();
        _diagnostics = diagnostics ?? DiagnosticHub.Sink;

        // Initialize managers
        Creatures = new CreatureManager(_diagnostics);
        Items = new ItemManager(_diagnostics);
        Orders = new HumanFortress.Simulation.Orders.OrdersManager(_diagnostics);
        Stockpiles = new HumanFortress.Simulation.Stockpile.StockpileManager();
        Reservations = new HumanFortress.Simulation.Jobs.ReservationManager();

        var zoneManager = new HumanFortress.Simulation.Zones.ZoneManager();
        Zones = new HumanFortress.Simulation.Zones.ZoneCoordinator(this, zoneManager);

        // Set self-reference
        Creatures.SetWorld(this);
    }

    internal IDiagnosticSink Diagnostics => _diagnostics;

    /// <summary>
    /// Serializes validate-and-commit topology transactions. Simulation topology
    /// writers hold this lock from their first authoritative read through the
    /// final dirty-set publication, so validation cannot be invalidated by a
    /// concurrent placeable/door/terrain writer.
    /// </summary>
    internal object TopologyLock => _topologyLock;

    internal void SetDiagnostics(IDiagnosticSink diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        Creatures.SetDiagnostics(diagnostics);
        Items.SetDiagnostics(diagnostics);
        Orders.SetDiagnostics(diagnostics);
    }

    /// <summary>
    /// Size of the world in chunks (N for N×N).
    /// </summary>
    internal int SizeInChunks => _sizeInChunks;

    /// <summary>
    /// Size of the world in tiles.
    /// </summary>
    internal int SizeInTiles => _sizeInChunks * Chunk.SIZE_XY;

    /// <summary>
    /// Maximum Z level.
    /// </summary>
    internal int MaxZ => _maxZ;

    /// <summary>
    /// Get or create a chunk.
    /// </summary>
    internal Chunk GetOrCreateChunk(ChunkKey key)
    {
        lock (_chunkLock)
        {
            if (!_chunks.TryGetValue(key, out var chunk))
            {
                chunk = new Chunk(key);
                _chunks.Add(key, chunk);
            }

            return chunk;
        }
    }

    /// <summary>
    /// Get chunk if it exists.
    /// </summary>
    internal Chunk? GetChunk(ChunkKey key)
    {
        lock (_chunkLock)
        {
            return _chunks.GetValueOrDefault(key);
        }
    }

    /// <summary>
    /// Get tile at world coordinates.
    /// </summary>
    internal TileBase? GetTile(int worldX, int worldY, int z)
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
    internal void SetTile(int worldX, int worldY, int z, TileBase tile, ulong tick)
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
    internal bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < SizeInTiles &&
               y >= 0 && y < SizeInTiles &&
               z >= 0 && z < _maxZ;
    }

    bool IWorldReader.IsValidPosition(int x, int y, int z)
    {
        return IsValidPosition(x, y, z);
    }

    /// <summary>
    /// Get all chunks in the world.
    /// </summary>
    internal IEnumerable<Chunk> GetAllChunks()
    {
        lock (_chunkLock)
        {
            return OrderChunks(_chunks.Values).ToArray();
        }
    }

    /// <summary>
    /// Get chunk at chunk coordinates.
    /// </summary>
    internal Chunk? GetChunkAt(int chunkX, int chunkY, int z)
    {
        return GetChunk(new ChunkKey(chunkX, chunkY, z));
    }

    /// <summary>
    /// Get all active chunks (L0/L1).
    /// </summary>
    internal IEnumerable<Chunk> GetActiveChunks()
    {
        lock (_chunkLock)
        {
            return OrderChunks(_chunks.Values.Where(static chunk => chunk.LODLevel <= 1)).ToArray();
        }
    }

    /// <summary>
    /// Mark chunk as dirty for navigation rebuild.
    /// </summary>
    internal void MarkChunkDirty(ChunkKey key)
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
            var list = OrderChunkKeys(_dirtyChunks).ToList();
            _dirtyChunks.Clear();
            return list;
        }
    }

    /// <summary>
    /// Update LOD levels based on player focus per SIM_LOD_POLICY.md.
    /// </summary>
    internal void UpdateLOD(int focusX, int focusY, int focusZ)
    {
        var focusChunkX = focusX / Chunk.SIZE_XY;
        var focusChunkY = focusY / Chunk.SIZE_XY;

        foreach (var chunk in GetAllChunks())
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
    internal IEnumerable<(ChunkKey key, EdgeBand band)> GetEdgeBandChunks()
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

    private static IOrderedEnumerable<ChunkKey> OrderChunkKeys(IEnumerable<ChunkKey> keys)
    {
        return keys
            .OrderBy(static key => key.Z)
            .ThenBy(static key => key.ChunkY)
            .ThenBy(static key => key.ChunkX);
    }

    private static IOrderedEnumerable<Chunk> OrderChunks(IEnumerable<Chunk> chunks)
    {
        return chunks
            .OrderBy(static chunk => chunk.Key.Z)
            .ThenBy(static chunk => chunk.Key.ChunkY)
            .ThenBy(static chunk => chunk.Key.ChunkX);
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
