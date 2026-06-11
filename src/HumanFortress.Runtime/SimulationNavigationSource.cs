using HumanFortress.Navigation;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

/// <summary>
/// Runtime adapter that exposes Simulation.World through navigation-owned
/// snapshot contracts.
/// </summary>
internal sealed class SimulationNavigationSource : INavigationWorldSource
{
    private readonly World _world;

    public SimulationNavigationSource(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public bool IsValid(Point3 position)
    {
        return _world.IsValidPosition(position.X, position.Y, position.Z);
    }

    public bool TryGetTile(Point3 position, out NavigationTile tile)
    {
        var source = _world.GetTile(position.X, position.Y, position.Z);
        if (!source.HasValue)
        {
            tile = default;
            return false;
        }

        tile = ToNavigationTile(source.Value);
        return true;
    }

    public bool TryGetChunk(HumanFortress.Navigation.ChunkKey key, out NavigationChunkSnapshot chunk)
    {
        var source = _world.GetChunk(new HumanFortress.Simulation.World.ChunkKey(key.ChunkX, key.ChunkY, key.Z));
        if (source == null)
        {
            chunk = default;
            return false;
        }

        chunk = ToNavigationChunk(source);
        return true;
    }

    public IEnumerable<NavigationChunkSnapshot> GetAllChunks()
    {
        foreach (var chunk in _world.GetAllChunks())
            yield return ToNavigationChunk(chunk);
    }

    public bool IsConstructionSiteAnchor(Point3 position)
    {
        if (!IsValid(position))
            return false;

        var chunkKey = new HumanFortress.Simulation.World.ChunkKey(
            position.X / HumanFortress.Simulation.World.Chunk.SIZE_XY,
            position.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY,
            position.Z);

        var chunk = _world.GetChunk(chunkKey);
        var placeables = chunk?.GetPlaceableData();
        if (placeables == null)
            return false;

        int localX = PositiveModulo(position.X, HumanFortress.Simulation.World.Chunk.SIZE_XY);
        int localY = PositiveModulo(position.Y, HumanFortress.Simulation.World.Chunk.SIZE_XY);
        int localIndex = HumanFortress.Simulation.World.Chunk.LocalIndex(localX, localY);

        return placeables.TryGetOwnedAt(localIndex, out var owned)
            && owned.ConstructionSite != null;
    }

    private static NavigationChunkSnapshot ToNavigationChunk(HumanFortress.Simulation.World.Chunk chunk)
    {
        var tiles = chunk.GetTilesCopy();
        var navTiles = new NavigationTile[tiles.Length];
        for (int i = 0; i < tiles.Length; i++)
            navTiles[i] = ToNavigationTile(tiles[i]);

        return new NavigationChunkSnapshot(
            new HumanFortress.Navigation.ChunkKey(chunk.Key.ChunkX, chunk.Key.ChunkY, chunk.Key.Z),
            navTiles,
            chunk.ConnectivityVersion);
    }

    private static NavigationTile ToNavigationTile(TileBase tile)
    {
        return new NavigationTile(
            ToNavigationTileKind(tile.Kind),
            tile.IsNatural,
            tile.IsWalkable,
            tile.IsStandable,
            tile.IsFlyable,
            tile.FluidDepth,
            tile.MetaBits);
    }

    private static NavigationTileKind ToNavigationTileKind(TerrainKind kind)
    {
        return kind switch
        {
            TerrainKind.SolidWall => NavigationTileKind.SolidWall,
            TerrainKind.OpenWithFloor => NavigationTileKind.OpenWithFloor,
            TerrainKind.OpenNoFloor => NavigationTileKind.OpenNoFloor,
            TerrainKind.Ramp => NavigationTileKind.Ramp,
            TerrainKind.Slope => NavigationTileKind.Slope,
            TerrainKind.StairsUp => NavigationTileKind.StairsUp,
            TerrainKind.StairsDown => NavigationTileKind.StairsDown,
            TerrainKind.StairsUD => NavigationTileKind.StairsUD,
            _ => NavigationTileKind.SolidWall,
        };
    }

    private static int PositiveModulo(int value, int divisor)
    {
        return ((value % divisor) + divisor) % divisor;
    }
}
