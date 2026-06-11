namespace HumanFortress.Navigation;

/// <summary>
/// Navigation-native tile categories used to build pathfinding caches without
/// depending on simulation terrain types.
/// </summary>
public enum NavigationTileKind : byte
{
    SolidWall = 0,
    OpenWithFloor = 1,
    OpenNoFloor = 2,
    Ramp = 3,
    Slope = 4,
    StairsUp = 5,
    StairsDown = 6,
    StairsUD = 7,
}

/// <summary>
/// Small immutable tile snapshot consumed by navigation cache builders.
/// </summary>
public readonly record struct NavigationTile(
    NavigationTileKind Kind,
    bool IsNatural,
    bool IsWalkable,
    bool IsStandable,
    bool IsFlyable,
    byte FluidDepth,
    byte MetaBits);

/// <summary>
/// Immutable per-chunk navigation input snapshot.
/// </summary>
public readonly record struct NavigationChunkSnapshot(
    ChunkKey Key,
    NavigationTile[] Tiles,
    ulong ConnectivityVersion);

/// <summary>
/// Read-only source of navigation-relevant world state.
/// Implemented by runtime adapters over the authoritative simulation world.
/// </summary>
public interface INavigationWorldSource
{
    bool IsValid(Point3 position);

    bool TryGetTile(Point3 position, out NavigationTile tile);

    bool TryGetChunk(ChunkKey key, out NavigationChunkSnapshot chunk);

    IEnumerable<NavigationChunkSnapshot> GetAllChunks();

    bool IsConstructionSiteAnchor(Point3 position);
}
