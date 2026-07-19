namespace HumanFortress.Contracts.Navigation;

/// <summary>
/// Result type for pathfinding operations.
/// </summary>
public enum PathResultKind : byte
{
    /// <summary>Complete path found to destination.</summary>
    Found = 0,

    /// <summary>Partial path found because the deterministic node budget was exhausted.</summary>
    Partial = 1,

    /// <summary>No path exists to destination.</summary>
    NoPath = 2,

    /// <summary>Invalid request (bad coordinates, sleeping zone, etc).</summary>
    Invalid = 3,

    /// <summary>The per-tick request budget was exhausted before search started.</summary>
    BudgetExhausted = 4,
}

/// <summary>
/// Immutable pathfinding request per NAVIGATION_SPEC.md section 5.4.
/// </summary>
public readonly record struct PathRequest(
    Point3 Source,
    Point3 Destination,
    MoveMode Mode,
    PathFlags Flags,
    uint Seed,
    byte SearchAttempt = 0)
{
    /// <summary>
    /// Maximum deterministic retry tier. With the current exponential policy this
    /// caps one search at 64 times the base node budget.
    /// </summary>
    public const byte MaxSearchAttempt = 6;

    /// <summary>
    /// Retry tier after applying the public deterministic cap.
    /// </summary>
    public byte EffectiveSearchAttempt => SearchAttempt > MaxSearchAttempt
        ? MaxSearchAttempt
        : SearchAttempt;

    /// <summary>
    /// Generate a deterministic hash for caching.
    /// </summary>
    public uint GetCacheKey()
    {
        unchecked
        {
            uint hash = 17;
            hash = hash * 31 + (uint)Source.X;
            hash = hash * 31 + (uint)Source.Y;
            hash = hash * 31 + (uint)Source.Z;
            hash = hash * 31 + (uint)Destination.X;
            hash = hash * 31 + (uint)Destination.Y;
            hash = hash * 31 + (uint)Destination.Z;
            hash = hash * 31 + (uint)Mode;
            hash = hash * 31 + (uint)Flags;
            hash = hash * 31 + Seed;
            hash = hash * 31 + EffectiveSearchAttempt;
            return hash;
        }
    }

    /// <summary>
    /// Return the same semantic request with a larger deterministic search budget.
    /// </summary>
    public PathRequest NextSearchAttempt()
    {
        return this with
        {
            SearchAttempt = EffectiveSearchAttempt >= MaxSearchAttempt
                ? MaxSearchAttempt
                : (byte)(EffectiveSearchAttempt + 1)
        };
    }
}

/// <summary>
/// A single node in a path.
/// </summary>
public readonly record struct PathNode(
    Point3 Position,
    ushort Cost)
{
    /// <summary>
    /// Convert to chunk key and local index.
    /// </summary>
    public (ChunkKey chunk, int localIdx) ToChunkLocal()
    {
        // Assuming 32x32 chunks
        const int ChunkSize = 32;
        int cx = Position.X / ChunkSize;
        int cy = Position.Y / ChunkSize;
        int localX = Position.X % ChunkSize;
        int localY = Position.Y % ChunkSize;
        int localIdx = localY * ChunkSize + localX;

        return (new ChunkKey(cx, cy, Position.Z), localIdx);
    }
}

/// <summary>
/// Chunk key for addressing chunks.
/// </summary>
public readonly record struct ChunkKey(int ChunkX, int ChunkY, int Z)
{
    public override string ToString() => $"({ChunkX},{ChunkY},{Z})";
}

/// <summary>
/// Path result from pathfinding service.
/// </summary>
public readonly record struct Path(
    PathResultKind Kind,
    int Length,
    uint TotalCost, // Fixed-point total cost (FP=10)
    uint Hash,
    ReadOnlyMemory<PathNode> Steps)
{
    /// <summary>
    /// Empty failed path.
    /// </summary>
    public static readonly Path Failed = new(PathResultKind.NoPath, 0, 0, 0, ReadOnlyMemory<PathNode>.Empty);

    /// <summary>
    /// Invalid path request.
    /// </summary>
    public static readonly Path Invalid = new(PathResultKind.Invalid, 0, 0, 0, ReadOnlyMemory<PathNode>.Empty);

    /// <summary>
    /// Search did not start because the deterministic per-tick request budget was exhausted.
    /// </summary>
    public static readonly Path BudgetExhausted = new(
        PathResultKind.BudgetExhausted,
        0,
        0,
        0,
        ReadOnlyMemory<PathNode>.Empty);

    /// <summary>
    /// True only for a complete path whose final node is the requested destination.
    /// </summary>
    public bool ReachesDestination(Point3 destination)
    {
        return Kind == PathResultKind.Found
            && Steps.Length > 0
            && Steps.Span[^1].Position == destination;
    }
}

/// <summary>
/// 3D point for navigation.
/// </summary>
public readonly record struct Point3(int X, int Y, int Z)
{
    public static Point3 Zero => new(0, 0, 0);

    public Point3 WithZ(int newZ) => new(X, Y, newZ);

    public int ManhattanDistance(Point3 other)
        => Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z);

    public int SquaredDistance(Point3 other)
    {
        int dx = X - other.X;
        int dy = Y - other.Y;
        int dz = Z - other.Z;
        return dx * dx + dy * dy + dz * dz;
    }
}
