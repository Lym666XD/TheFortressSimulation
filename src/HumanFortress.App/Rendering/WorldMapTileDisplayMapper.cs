using HumanFortress.Contracts.WorldGen;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

internal readonly record struct WorldMapTileDisplay(int Glyph, Color Color);

internal static class WorldMapTileDisplayMapper
{
    public static WorldMapTileDisplay FromTile(WorldMapTileView tile)
    {
        return tile.DisplayKind switch
        {
            WorldMapBiomeDisplayKind.Ocean => new('~', Color.DarkBlue),
            WorldMapBiomeDisplayKind.Lake => new('~', Color.Blue),
            WorldMapBiomeDisplayKind.River => new('~', Color.Cyan),
            WorldMapBiomeDisplayKind.Mountain => new('^', Color.Gray),
            WorldMapBiomeDisplayKind.Hills => new('n', Color.Brown),
            WorldMapBiomeDisplayKind.Desert => new('.', Color.Yellow),
            WorldMapBiomeDisplayKind.Tundra => new('.', Color.White),
            WorldMapBiomeDisplayKind.Glacier => new('#', Color.Cyan),
            WorldMapBiomeDisplayKind.TemperateForest => new('T', Color.Green),
            WorldMapBiomeDisplayKind.TropicalForest => new('T', Color.DarkGreen),
            WorldMapBiomeDisplayKind.Taiga => new('t', Color.DarkGreen),
            WorldMapBiomeDisplayKind.TemperateGrassland => new('.', Color.LightGreen),
            WorldMapBiomeDisplayKind.Savanna => new(':', Color.YellowGreen),
            WorldMapBiomeDisplayKind.Swamp => new('%', Color.DarkGreen),
            _ => new('?', Color.Magenta)
        };
    }
}
