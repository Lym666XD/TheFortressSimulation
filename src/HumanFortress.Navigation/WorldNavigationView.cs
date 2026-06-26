using HumanFortress.Contracts.Navigation;
namespace HumanFortress.Navigation;

/// <summary>
/// Concrete IWorldNavigationView over a NavigationManager cache and a navigation world source.
/// </summary>
internal sealed class WorldNavigationView : IWorldNavigationView
{
    private readonly NavigationManager _nav;
    private readonly INavigationWorldSource _source;

    internal WorldNavigationView(NavigationManager nav)
    {
        _nav = nav;
        _source = nav.Source;
    }

    internal bool IsValid(Point3 position)
        => _source.IsValid(position);

    internal NavCapability GetCapabilities(Point3 position)
    {
        var navData = _nav.GetNavDataAt(position.X, position.Y, position.Z);
        if (navData == null) return NavCapability.None;
        var localIdx = LocalIndex(position);
        if (localIdx < 0 || localIdx >= ChunkNavData.TilesPerChunk) return NavCapability.None;
        return (NavCapability)navData.NavMask[localIdx];
    }

    internal ushort GetCost(Point3 position)
    {
        var navData = _nav.GetNavDataAt(position.X, position.Y, position.Z);
        if (navData == null) return ushort.MaxValue;
        var localIdx = LocalIndex(position);
        if (localIdx < 0 || localIdx >= ChunkNavData.TilesPerChunk) return ushort.MaxValue;
        return navData.NavCost[localIdx];
    }

    internal bool IsWalkable(Point3 position, MoveMode mode)
    {
        // Allow traversal through construction site anchors (avoid partitioning),
        // but disallow standing on them in IsStandable.
        var caps = GetCapabilities(position);
        return mode switch
        {
            MoveMode.Walk => (caps & NavCapability.Walk) != 0,
            MoveMode.Swim => (caps & NavCapability.Swim) != 0,
            MoveMode.Fly => (caps & NavCapability.Fly) != 0,
            _ => false,
        };
    }

    internal bool HasStairsUp(Point3 position)
    {
        if (!_source.TryGetTile(position, out var tile)) return false;
        return tile.Kind == NavigationTileKind.StairsUp || tile.Kind == NavigationTileKind.StairsUD;
    }

    internal bool HasStairsDown(Point3 position)
    {
        if (!_source.TryGetTile(position, out var tile)) return false;
        return tile.Kind == NavigationTileKind.StairsDown || tile.Kind == NavigationTileKind.StairsUD;
    }

    internal int GetConnectivityVersion(ChunkKey chunk)
    {
        var nav = _nav.GetNavData(chunk);
        return nav?.ConnectivityVersion ?? 0;
    }

    internal bool TryGetRampDirection(Point3 position, out byte rampDirection)
    {
        rampDirection = 0;
        return false;
    }

    internal bool IsStandable(Point3 position)
    {
        // Treat construction site anchor as not standable (cannot end movement here)
        if (_source.IsConstructionSiteAnchor(position)) return false;
        // Prefer capabilities; fall back to tile kind
        var caps = GetCapabilities(position);
        if ((caps & NavCapability.Standable) != 0) return true;
        return _source.TryGetTile(position, out var tile) && tile.Kind == NavigationTileKind.OpenWithFloor;
    }

    internal bool TryGetDownRampDirection(Point3 position, out byte rampDirection)
    {
        rampDirection = 0;
        return false;
    }

    internal bool TryGetUpRampMask(Point3 position, out byte mask)
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

    bool IWorldNavigationView.IsValid(Point3 position) => IsValid(position);

    NavCapability IWorldNavigationView.GetCapabilities(Point3 position) => GetCapabilities(position);

    ushort IWorldNavigationView.GetCost(Point3 position) => GetCost(position);

    bool IWorldNavigationView.IsWalkable(Point3 position, MoveMode mode) => IsWalkable(position, mode);

    bool IWorldNavigationView.HasStairsUp(Point3 position) => HasStairsUp(position);

    bool IWorldNavigationView.HasStairsDown(Point3 position) => HasStairsDown(position);

    int IWorldNavigationView.GetConnectivityVersion(ChunkKey chunk) => GetConnectivityVersion(chunk);

    bool IWorldNavigationView.IsStandable(Point3 position) => IsStandable(position);

    bool IWorldNavigationView.TryGetUpRampMask(Point3 position, out byte mask) =>
        TryGetUpRampMask(position, out mask);
}
