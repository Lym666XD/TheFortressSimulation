using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Simulation.World;

/// <summary>
/// Fixed-size chunk of tiles per CHUNK_AND_DATA_LAYOUT.md.
/// Default 32x32 cells per Z-level.
/// </summary>
public sealed class Chunk
{
    public const int SIZE_XY = 32;
    public const int CELLS_PER_LAYER = SIZE_XY * SIZE_XY; // 1024

    private readonly TileBase[] _tiles;
    private readonly Dictionary<int, FurnitureCell> _furniture;
    private readonly Dictionary<int, List<FieldCell>> _fields;
    private readonly Dictionary<int, List<ItemStackRef>> _items;
    private readonly object _writeLock = new();

    public ChunkKey Key { get; }
    public int LODLevel { get; set; }
    public ulong LastModifiedTick { get; private set; }
    public ulong ConnectivityVersion { get; private set; }

    public Chunk(ChunkKey key)
    {
        Key = key;
        _tiles = new TileBase[CELLS_PER_LAYER];
        _furniture = new Dictionary<int, FurnitureCell>();
        _fields = new Dictionary<int, List<FieldCell>>();
        _items = new Dictionary<int, List<ItemStackRef>>();
        LODLevel = 0;
    }

    /// <summary>
    /// Get tile at local coordinates. Thread-safe for reads.
    /// </summary>
    public TileBase GetTile(int x, int y)
    {
        if (x < 0 || x >= SIZE_XY || y < 0 || y >= SIZE_XY)
            throw new ArgumentOutOfRangeException();

        int index = y * SIZE_XY + x;
        return _tiles[index];
    }

    /// <summary>
    /// Set tile at local coordinates. Must be called only during Write phase.
    /// </summary>
    public void SetTile(int x, int y, TileBase tile, ulong tick)
    {
        if (x < 0 || x >= SIZE_XY || y < 0 || y >= SIZE_XY)
            throw new ArgumentOutOfRangeException();

        lock (_writeLock)
        {
            int index = y * SIZE_XY + x;
            var oldTile = _tiles[index];
            _tiles[index] = tile;
            LastModifiedTick = tick;

            // Update connectivity version if passability changed
            if (oldTile.IsWalkable != tile.IsWalkable)
            {
                ConnectivityVersion++;
            }
        }
    }

    /// <summary>
    /// Get furniture at position. Thread-safe.
    /// </summary>
    public FurnitureCell? GetFurniture(int x, int y)
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
    public void PlaceFurniture(int x, int y, FurnitureRef furniture, bool isBlocker, ulong tick)
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
    public TileBase[] GetTilesCopy()
    {
        var copy = new TileBase[CELLS_PER_LAYER];
        Array.Copy(_tiles, copy, CELLS_PER_LAYER);
        return copy;
    }

    /// <summary>
    /// Convert local coordinates to index.
    /// </summary>
    public static int LocalIndex(int x, int y) => y * SIZE_XY + x;

    /// <summary>
    /// Convert index to local coordinates.
    /// </summary>
    public static (int x, int y) IndexToLocal(int index)
    {
        return (index % SIZE_XY, index / SIZE_XY);
    }
}

/// <summary>
/// Unique chunk identifier.
/// </summary>
public readonly struct ChunkKey : IEquatable<ChunkKey>
{
    public readonly int ChunkX;
    public readonly int ChunkY;
    public readonly int Z;

    public ChunkKey(int chunkX, int chunkY, int z)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        Z = z;
    }

    public bool Equals(ChunkKey other)
    {
        return ChunkX == other.ChunkX && ChunkY == other.ChunkY && Z == other.Z;
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
public sealed class FurnitureCell
{
    public FurnitureRef? Blocker { get; set; }
    public List<FurnitureRef>? Passables { get; set; }
    public byte ConnectMaskNESW { get; set; }
    public byte OpacityMaskNESW { get; set; }
}

/// <summary>
/// Reference to furniture instance.
/// </summary>
public readonly struct FurnitureRef
{
    public readonly int Id;
    public readonly ushort TypeId;

    public FurnitureRef(int id, ushort typeId)
    {
        Id = id;
        TypeId = typeId;
    }
}

/// <summary>
/// L4 overlay - fields (gases/decals).
/// </summary>
public sealed class FieldCell
{
    public ushort Id { get; set; }
    public byte Intensity { get; set; }
    public ushort Age { get; set; }
}

/// <summary>
/// L5 overlay - item stacks.
/// </summary>
public readonly struct ItemStackRef
{
    public readonly int Handle;

    public ItemStackRef(int handle)
    {
        Handle = handle;
    }
}