using SadRogue.Primitives;
using HumanFortress.App.WorldGeneration;
using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.App.Session;

/// <summary>
/// Per-run state handed between app screens while preparing a fortress session.
/// </summary>
internal sealed partial class FortressSessionContext
{
    internal FortressSessionContext(bool autoDig)
    {
        AutoDig = autoDig;
        FortressSize = FortressSessionSizeRules.DefaultFortressSize;
    }

    internal bool AutoDig { get; }
    internal IGeneratedWorldData CurrentWorld { get; private set; } = EmptyGeneratedWorldData.Instance;
    internal Point SelectedTile { get; private set; }
    internal Point EmbarkLocation { get; private set; }
    internal int FortressSize { get; private set; }

    internal void SetGeneratedWorld(IGeneratedWorldData? result)
    {
        CurrentWorld = result ?? EmptyGeneratedWorldData.Instance;
        SelectedTile = default;
        EmbarkLocation = default;
        FortressSize = FortressSessionSizeRules.DefaultFortressSize;
    }

    internal void SelectEmbarkTile(Point tile)
    {
        SelectedTile = ClampToWorld(tile);
    }

    internal void ConfigureEmbark(Point location, int fortressSize)
    {
        EmbarkLocation = ClampToWorld(location);
        FortressSize = FortressSessionSizeRules.Normalize(fortressSize);
    }

}
