using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;
using TerrainKind = HumanFortress.Simulation.Tiles.TerrainKind;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class MapViewportSnapshotBuilder
{
    private static (int Glyph, Color Color) GetTileDisplay(TileBase tile, IRuntimeGeologyCatalog? geologyCatalog)
    {
        var geology = geologyCatalog?.GetGeologyByHandle(tile.GeoMatId);
        var color = geology != null
            ? new Color(geology.Display.Foreground.R, geology.Display.Foreground.G, geology.Display.Foreground.B)
            : Color.Gray;

        return tile.Kind switch
        {
            TerrainKind.SolidWall => (geology?.Display.Glyph ?? '#', color),
            TerrainKind.OpenWithFloor => GetOpenWithFloorDisplay(tile, color),
            TerrainKind.OpenNoFloor => (' ', color),
            TerrainKind.Ramp => ('^', color),
            TerrainKind.StairsUp => ('<', color),
            TerrainKind.StairsDown => ('>', color),
            TerrainKind.StairsUD => ('X', color),
            _ => (geology?.Display.Glyph ?? '?', color),
        };
    }

    private static (int Glyph, Color Color) GetOpenWithFloorDisplay(TileBase tile, Color fallbackColor)
    {
        if (tile.HasSnow)
            return ('*', Color.White);

        if (tile.HasGrass)
            return (',', Color.Green);

        if (tile.HasMud)
            return ('~', Color.DarkGoldenrod);

        if (tile.HasMoss)
            return (',', new Color(0, 120, 0));

        return ('.', fallbackColor);
    }
}
