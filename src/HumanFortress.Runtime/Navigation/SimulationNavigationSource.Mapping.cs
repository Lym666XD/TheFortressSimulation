using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Runtime.Navigation;

internal sealed partial class SimulationNavigationSource
{
    private static NavigationChunkSnapshot ToNavigationChunk(HumanFortress.Simulation.World.Chunk chunk)
    {
        var tiles = chunk.GetTilesCopy();
        var navTiles = new NavigationTile[tiles.Length];
        for (int i = 0; i < tiles.Length; i++)
            navTiles[i] = ToNavigationTile(tiles[i]);

        return new NavigationChunkSnapshot(
            new HumanFortress.Contracts.Navigation.ChunkKey(chunk.Key.ChunkX, chunk.Key.ChunkY, chunk.Key.Z),
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
}
