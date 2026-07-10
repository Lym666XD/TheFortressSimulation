using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Zones;
using HumanFortress.Simulation.Placeables;

namespace HumanFortress.Simulation.World;

/// <summary>
/// Fixed-size chunk of tiles per CHUNK_AND_DATA_LAYOUT.md.
/// Default 32x32 cells per Z-level.
/// </summary>
internal sealed class Chunk
{
    internal const int SIZE_XY = 32;
    internal const int CELLS_PER_LAYER = SIZE_XY * SIZE_XY; // 1024

    private readonly TileBase[] _tiles;
    private readonly Dictionary<int, FurnitureCell> _furniture;
    private readonly Dictionary<int, List<FieldCell>> _fields;
    private readonly Dictionary<int, List<ItemStackRef>> _items;
    private readonly SortedSet<int> _dirtyTiles;
    private readonly object _writeLock = new();
    private ChunkStockpileData? _stockpileData;
    private ChunkZoneData? _zoneData;
    private ChunkPlaceableData? _placeableData;

    internal ChunkKey Key { get; }
    internal int LODLevel { get; set; }
    internal ulong LastModifiedTick { get; private set; }
    internal ulong ConnectivityVersion { get; private set; }

    internal Chunk(ChunkKey key)
    {
        Key = key;
        _tiles = new TileBase[CELLS_PER_LAYER];
        _furniture = new Dictionary<int, FurnitureCell>();
        _fields = new Dictionary<int, List<FieldCell>>();
        _items = new Dictionary<int, List<ItemStackRef>>();
        _dirtyTiles = new SortedSet<int>();
        _stockpileData = new ChunkStockpileData();
        LODLevel = 0;
    }

    /// <summary>
    /// Get tile at local coordinates. Synchronized with write-phase tile replacement
    /// so readers never observe a torn multi-field TileBase value.
    /// </summary>
    internal TileBase GetTile(int x, int y)
    {
        if (x < 0 || x >= SIZE_XY || y < 0 || y >= SIZE_XY)
            throw new ArgumentOutOfRangeException();

        int index = y * SIZE_XY + x;
        lock (_writeLock)
        {
            return _tiles[index];
        }
    }

    /// <summary>
    /// Set tile at local coordinates. Must be called only during Write phase.
    /// </summary>
    internal void SetTile(int x, int y, TileBase tile, ulong tick)
    {
        if (x < 0 || x >= SIZE_XY || y < 0 || y >= SIZE_XY)
            throw new ArgumentOutOfRangeException();

        lock (_writeLock)
        {
            int index = y * SIZE_XY + x;
            var oldTile = _tiles[index];
            _tiles[index] = tile;
            _dirtyTiles.Add(index);
            LastModifiedTick = tick;

            // Update connectivity version if topology-relevant properties changed
            // Bump on TerrainKind change or Walkable change to invalidate derived caches deterministically
            if (oldTile.Kind != tile.Kind || oldTile.IsWalkable != tile.IsWalkable)
            {
                ConnectivityVersion++;
            }
        }
    }

    /// <summary>
    /// Get furniture at position. Thread-safe.
    /// </summary>
    internal FurnitureCell? GetFurniture(int x, int y)
    {
        int index = y * SIZE_XY + x;
        lock (_writeLock)
        {
            return _furniture.GetValueOrDefault(index);
        }
    }

    /// <summary>
    /// Place furniture. Must be called only during Write phase.
    /// </summary>
    internal void PlaceFurniture(int x, int y, FurnitureRef furniture, bool isBlocker, ulong tick)
    {
        int index = y * SIZE_XY + x;
        lock (_writeLock)
        {
            if (!_furniture.TryGetValue(index, out var cell))
            {
                cell = new FurnitureCell();
                _furniture[index] = cell;
            }

            if (isBlocker)
            {
                cell.Blocker = furniture;
                ConnectivityVersion++;
            }
            else
            {
                cell.Passables ??= new List<FurnitureRef>();
                cell.Passables.Add(furniture);
            }

            LastModifiedTick = tick;
        }
    }

    /// <summary>
    /// Get all tiles for batch operations. Creates a copy for thread safety.
    /// </summary>
    internal TileBase[] GetTilesCopy()
    {
        var copy = new TileBase[CELLS_PER_LAYER];
        lock (_writeLock)
        {
            Array.Copy(_tiles, copy, CELLS_PER_LAYER);
        }
        return copy;
    }

    /// <summary>
    /// Convert local coordinates to index.
    /// </summary>
    internal static int LocalIndex(int x, int y) => y * SIZE_XY + x;

    /// <summary>
    /// Convert index to local coordinates.
    /// </summary>
    internal static (int x, int y) IndexToLocal(int index)
    {
        return (index % SIZE_XY, index / SIZE_XY);
    }

    /// <summary>
    /// Get stockpile data for this chunk. Thread-safe for reads.
    /// </summary>
    internal ChunkStockpileData? GetStockpileData()
    {
        return _stockpileData;
    }

    /// <summary>
    /// Initialize stockpile data if not present. Write phase only.
    /// </summary>
    internal void EnsureStockpileData()
    {
        lock (_writeLock)
        {
            _stockpileData ??= new ChunkStockpileData();
        }
    }

    /// <summary>
    /// Get zone data for this chunk. Thread-safe for reads.
    /// </summary>
    internal ChunkZoneData? GetZoneData()
    {
        return _zoneData;
    }

    /// <summary>
    /// Initialize zone data if not present. Write phase only.
    /// </summary>
    internal void EnsureZoneData()
    {
        lock (_writeLock)
        {
            _zoneData ??= new ChunkZoneData();
        }
    }

    /// <summary>
    /// Get placeable data for this chunk. Thread-safe for reads.
    /// NOTE: PlaceableData will be serialized to chunk saves when save system is implemented.
    /// Currently stored in memory only.
    /// </summary>
    internal ChunkPlaceableData? GetPlaceableData()
    {
        return _placeableData;
    }

    /// <summary>
    /// Initialize placeable data if not present. Write phase only.
    /// </summary>
    internal void EnsurePlaceableData()
    {
        lock (_writeLock)
        {
            _placeableData ??= new ChunkPlaceableData();
        }
    }

    /// <summary>
    /// Remove a furniture reference at a cell matching the given placeable GUID.
    /// Returns true if a blocking ref was removed (connectivity changed).
    /// </summary>
    internal bool RemoveFurnitureAt(int x, int y, Guid placeableGuid, ulong tick)
    {
        if (x < 0 || x >= SIZE_XY || y < 0 || y >= SIZE_XY)
            throw new ArgumentOutOfRangeException();

        lock (_writeLock)
        {
            int index = y * SIZE_XY + x;
            bool blockerRemoved = false;
            if (_furniture.TryGetValue(index, out var cell))
            {
                if (cell.Blocker?.IsPlaceable == true && cell.Blocker.Value.PlaceableGuid == placeableGuid)
                {
                    cell.Blocker = null;
                    blockerRemoved = true;
                }

                if (cell.Passables != null)
                {
                    cell.Passables.RemoveAll(fr => fr.IsPlaceable && fr.PlaceableGuid == placeableGuid);
                    if (cell.Passables.Count == 0) cell.Passables = null;
                }

                if (blockerRemoved)
                {
                    ConnectivityVersion++;
                }
                LastModifiedTick = tick;
            }

            return blockerRemoved;
        }
    }

    /// <summary>
    /// Bump connectivity version to invalidate derived caches.
    /// Called when L0 or L2 topology changes (tiles, furniture, placeables).
    /// </summary>
    internal void BumpConnectivityVersion()
    {
        lock (_writeLock)
        {
            ConnectivityVersion++;
        }
    }

    /// <summary>
    /// Mark tile and neighbors dirty for cache rebuild.
    /// Called when L0 or L2 changes affect pathfinding/LOS.
    /// </summary>
    internal void MarkTileDirty(int localIndex, ulong tick)
    {
        if (localIndex < 0 || localIndex >= CELLS_PER_LAYER)
            throw new ArgumentOutOfRangeException(nameof(localIndex));

        lock (_writeLock)
        {
            LastModifiedTick = tick;
            _dirtyTiles.Add(localIndex);
            ConnectivityVersion++;
        }
    }

    /// <summary>
    /// Drain dirty tile indexes in stable local-index order.
    /// </summary>
    internal int[] DrainDirtyTileIndices()
    {
        lock (_writeLock)
        {
            var dirty = _dirtyTiles.ToArray();
            _dirtyTiles.Clear();
            return dirty;
        }
    }
}

/// <summary>
/// Unique chunk identifier.
/// </summary>
internal readonly struct ChunkKey : IEquatable<ChunkKey>
{
    internal readonly int ChunkX;
    internal readonly int ChunkY;
    internal readonly int Z;

    internal ChunkKey(int chunkX, int chunkY, int z)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        Z = z;
    }

    internal bool Equals(ChunkKey other)
    {
        return ChunkX == other.ChunkX && ChunkY == other.ChunkY && Z == other.Z;
    }

    bool IEquatable<ChunkKey>.Equals(ChunkKey other)
    {
        return Equals(other);
    }

    public override bool Equals(object? obj)
    {
        return obj is ChunkKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ChunkX, ChunkY, Z);
    }

    public override string ToString()
    {
        return $"Chunk({ChunkX},{ChunkY},{Z})";
    }
}

/// <summary>
/// L2 overlay - furniture/constructions.
/// </summary>
internal sealed class FurnitureCell
{
    internal FurnitureRef? Blocker { get; set; }
    internal List<FurnitureRef>? Passables { get; set; }
    internal byte ConnectMaskNESW { get; set; }
    internal byte OpacityMaskNESW { get; set; }
}

/// <summary>
/// Reference to furniture/placeable instance.
/// MIGRATION: Old system uses Id + TypeId, new system uses PlaceableGuid.
/// </summary>
internal readonly struct FurnitureRef
{
    internal readonly int Id;  // Legacy: numeric ID
    internal readonly ushort TypeId;  // Legacy: type ID
    internal readonly Guid PlaceableGuid;  // New: GUID reference to PlaceableInstance

    // Legacy constructor (will be deprecated)
    internal FurnitureRef(int id, ushort typeId)
    {
        Id = id;
        TypeId = typeId;
        PlaceableGuid = Guid.Empty;
    }

    // New constructor (use this for placeables)
    internal FurnitureRef(Guid placeableGuid)
    {
        Id = 0;
        TypeId = 0;
        PlaceableGuid = placeableGuid;
    }

    internal bool IsPlaceable => PlaceableGuid != Guid.Empty;
}

/// <summary>
/// L4 overlay - fields (gases/decals).
/// </summary>
internal sealed class FieldCell
{
    internal ushort Id { get; set; }
    internal byte Intensity { get; set; }
    internal ushort Age { get; set; }
}

/// <summary>
/// L5 overlay - item stacks.
/// </summary>
internal readonly struct ItemStackRef
{
    internal readonly int Handle;

    internal ItemStackRef(int handle)
    {
        Handle = handle;
    }
}
