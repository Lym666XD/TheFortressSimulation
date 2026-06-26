using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Core.World;
using HumanFortress.WorldGen;
using SadRogue.Primitives;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    RuntimeFortressGenerationResult IFortressRuntimeSessionBootstrapPort.GenerateAndFillFortressWorld(
        RuntimeFortressGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = _runtimeContentSnapshot;
        if (content == null)
            return new RuntimeFortressGenerationResult(
                RuntimeFortressGenerationStatus.MissingGenerationContent,
                FortressMapSize: 0);

        var embarkLocation = new Point(request.EmbarkX, request.EmbarkY);
        var generator = new FortressGenerator(
            request.FortressSize,
            CreateWorldTile(request),
            embarkLocation,
            CreateFortressSeed(embarkLocation),
            CreateFortressGenerationContent(content));

        var fortressMap = generator.Generate();
        _log($"[GenerateFortressMap] Fortress map generated: {fortressMap.Size}x{fortressMap.Size} chunks");

        if (!FillRuntimeWorld(fortressMap.FillWorld))
        {
            return new RuntimeFortressGenerationResult(
                RuntimeFortressGenerationStatus.MissingRuntimeWorld,
                fortressMap.Size);
        }

        return new RuntimeFortressGenerationResult(
            RuntimeFortressGenerationStatus.Success,
            fortressMap.Size);
    }

    private static WorldTile CreateWorldTile(RuntimeFortressGenerationRequest request)
    {
        return new WorldTile
        {
            BiomeId = request.BiomeId,
            Elevation = request.Elevation,
            Temperature = request.Temperature,
            Rainfall = request.Rainfall,
            Drainage = request.Drainage,
            RiverClass = request.RiverClass,
            HasAquifer = request.HasAquifer,
            StoneSet = request.StoneSet.ToArray(),
            LandmarkIds = request.LandmarkIds.ToArray()
        };
    }

    private static uint CreateFortressSeed(Point embarkLocation)
    {
        return (uint)(embarkLocation.X * 1000 + embarkLocation.Y);
    }

    private static FortressGenerationContent CreateFortressGenerationContent(FortressRuntimeContentSnapshot content)
    {
        return new FortressGenerationContent(
            content.Geology,
            content.MapgenTuningJson,
            content.OreTuningJson,
            content.CavernTuningJson);
    }
}
