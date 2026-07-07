using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.WorldGen;
using SadRogue.Primitives;

namespace HumanFortress.App.Session;

internal sealed partial class FortressSessionInitializer
{
    private static RuntimeFortressGenerationRequest CreateGenerationRequest(
        int fortressSize,
        WorldTileSnapshot worldTile,
        Point embarkLocation)
    {
        return new RuntimeFortressGenerationRequest(
            fortressSize,
            embarkLocation.X,
            embarkLocation.Y,
            worldTile.BiomeId,
            worldTile.Elevation,
            worldTile.Temperature,
            worldTile.Rainfall,
            worldTile.Drainage,
            worldTile.RiverClass,
            worldTile.HasAquifer,
            worldTile.StoneSet,
            worldTile.LandmarkIds);
    }

    private static EmbarkSiteSummary CreateEmbarkSiteSummary(
        Point embarkLocation,
        WorldTileSnapshot worldTile)
    {
        return new EmbarkSiteSummary(
            embarkLocation,
            worldTile.BiomeName,
            worldTile.Elevation);
    }
}
