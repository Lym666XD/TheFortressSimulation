using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;

namespace HumanFortress.Navigation;

/// <summary>
/// Concrete IWorldNavigationView over Simulation.World + NavigationManager caches.
/// </summary>
public sealed class WorldNavigationView : IWorldNavigationView
{
    private readonly NavigationManager _nav;
    private readonly World _world;

    public WorldNavigationView(NavigationManager nav, World world)
    {
        _nav = nav;
        _world = world;
    }

    public bool IsValid(Point3 position)
        => _world.IsValidPosition(position.X, position.Y, position.Z);

    public NavCapability GetCapabilities(Point3 position)
    {
        var navData = _nav.GetNavDataAt(position.X, position.Y, position.Z);
        if (navData == null) return NavCapability.None;
        var localIdx = LocalIndex(position);
        if (localIdx < 0 || localIdx >= ChunkNavData.TilesPerChunk) return NavCapability.None;
        return (NavCapability)navData.NavMask[localIdx];
    }

    public ushort GetCost(Point3 position)
    {
        var navData = _nav.GetNavDataAt(position.X, position.Y, position.Z);
        if (navData == null) return ushort.MaxValue;
        var localIdx = LocalIndex(position);
        if (localIdx < 0 || localIdx >= ChunkNavData.TilesPerChunk) return ushort.MaxValue;
        return navData.NavCost[localIdx];
    }

    public bool IsWalkable(Point3 position, MoveMode mode)
    {
        var caps = GetCapabilities(position);
        return mode switch
        {
            MoveMode.Walk => (caps & NavCapability.Walk) != 0,
            MoveMode.Swim => (caps & NavCapability.Swim) != 0,
            MoveMode.Fly => (caps & NavCapability.Fly) != 0,
            _ => false,
        };
    }

    public bool HasStairsUp(Point3 position)
    {
        var tile = _world.GetTile(position.X, position.Y, position.Z);
        if (tile == null) return false;
        return tile.Value.Kind == TerrainKind.StairsUp || tile.Value.Kind == TerrainKind.StairsUD;
    }

    public bool HasStairsDown(Point3 position)
    {
        var tile = _world.GetTile(position.X, position.Y, position.Z);
        if (tile == null) return false;
        return tile.Value.Kind == TerrainKind.StairsDown || tile.Value.Kind == TerrainKind.StairsUD;
    }

    public int GetConnectivityVersion(ChunkKey chunk)
    {
        var nav = _nav.GetNavData(chunk);
        return nav?.ConnectivityVersion ?? 0;
    }

    public bool TryGetRampDirection(Point3 position, out byte rampDirection)
    {
        rampDirection = 0;
        var navData = _nav.GetNavDataAt(position.X, position.Y, position.Z);
        if (navData == null) return false;
        int idx = LocalIndex(position);
        if (idx < 0 || idx >= ChunkNavData.TilesPerChunk) return false;
        byte dir = navData.UpRampDir[idx];
        if (dir == 255) return false;
        rampDirection = dir;
        return true;
    }

    public bool IsStandable(Point3 position)
    {
        // Prefer capabilities; fall back to tile kind
        var caps = GetCapabilities(position);
        if ((caps & NavCapability.Standable) != 0) return true;
        var tile = _world.GetTile(position.X, position.Y, position.Z);
        return tile.HasValue && (tile.Value.Kind == TerrainKind.OpenWithFloor || tile.Value.Kind == TerrainKind.Slope);
    }

    public bool TryGetDownRampDirection(Point3 position, out byte rampDirection)
    {
        rampDirection = 0;
        var navData = _nav.GetNavDataAt(position.X, position.Y, position.Z);
        if (navData == null) return false;
        int idx = LocalIndex(position);
        if (idx < 0 || idx >= ChunkNavData.TilesPerChunk) return false;
        byte dir = navData.DownRampDir[idx];
        if (dir == 255) return false;
        rampDirection = dir;
        return true;
    }

    public bool TryGetUpRampMask(Point3 position, out byte mask)
    {
        mask = 0;
        var navData = _nav.GetNavDataAt(position.X, position.Y, position.Z);
        if (navData == null) return false;
        int idx = LocalIndex(position);
        if (idx < 0 || idx >= ChunkNavData.TilesPerChunk) return false;
        var m = navData.UpRampMask[idx];
        if (m == 0) return false;
        mask = m;
        return true;
    }

    private static int LocalIndex(Point3 position)
    {
        const int ChunkSize = 32;
        int localX = ((position.X % ChunkSize) + ChunkSize) % ChunkSize;
        int localY = ((position.Y % ChunkSize) + ChunkSize) % ChunkSize;
        return localY * ChunkSize + localX;
    }
}
