using HumanFortress.WorldGen;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Per-run state handed between app screens while preparing a fortress session.
/// </summary>
public sealed class FortressSessionContext
{
    private const int DefaultFortressSize = 2;
    private const int MinFortressSize = 2;
    private const int MaxFortressSize = 8;

    public FortressSessionContext(bool autoDig)
    {
        AutoDig = autoDig;
        FortressSize = DefaultFortressSize;
    }

    public bool AutoDig { get; }
    public WorldGenResult CurrentWorld { get; private set; }
    public Point SelectedTile { get; private set; }
    public Point EmbarkLocation { get; private set; }
    public int FortressSize { get; private set; }

    public void SetGeneratedWorld(WorldGenResult result)
    {
        CurrentWorld = result;
        SelectedTile = default;
        EmbarkLocation = default;
        FortressSize = DefaultFortressSize;
    }

    public void SelectEmbarkTile(Point tile)
    {
        SelectedTile = ClampToWorld(tile);
    }

    public void ConfigureEmbark(Point location, int fortressSize)
    {
        EmbarkLocation = ClampToWorld(location);
        FortressSize = ClampFortressSize(fortressSize);
    }

    private static int ClampFortressSize(int fortressSize)
    {
        return Math.Clamp(fortressSize, MinFortressSize, MaxFortressSize);
    }

    private Point ClampToWorld(Point tile)
    {
        var tiles = CurrentWorld.Tiles;
        if (tiles == null)
            return tile;

        int width = tiles.GetLength(0);
        int height = tiles.GetLength(1);
        if (width <= 0 || height <= 0)
            return default;

        return new Point(
            Math.Clamp(tile.X, 0, width - 1),
            Math.Clamp(tile.Y, 0, height - 1));
    }
}
