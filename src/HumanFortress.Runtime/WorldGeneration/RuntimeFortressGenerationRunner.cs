using HumanFortress.Content.Loading;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Core.World;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen.Implementation;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.WorldGeneration;

internal static class RuntimeFortressGenerationRunner
{
    internal static RuntimeFortressGenerationResult GenerateAndFill(
        RuntimeFortressGenerationRequest request,
        FortressRuntimeContentSnapshot? content,
        Func<Action<World>, bool> fillRuntimeWorld,
        Action<string>? log,
        IDiagnosticSink? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(fillRuntimeWorld);

        if (content == null)
            return new RuntimeFortressGenerationResult(
                RuntimeFortressGenerationStatus.MissingGenerationContent,
                FortressMapSize: 0);

        var embarkLocation = new Point(request.EmbarkX, request.EmbarkY);
        var generator = new FortressGenerator(
            request.FortressSize,
            CreateWorldTile(request),
            embarkLocation,
            CreateFortressSeed(request),
            CreateFortressGenerationContent(content),
            diagnostics);

        var fortressMap = generator.Generate();
        log?.Invoke($"[GenerateFortressMap] Fortress map generated: {fortressMap.Size}x{fortressMap.Size} chunks");

        if (!fillRuntimeWorld(fortressMap.FillWorld))
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

    private static uint CreateFortressSeed(RuntimeFortressGenerationRequest request)
    {
        return request.GenerationSeed
            ?? unchecked((uint)(request.EmbarkX * 1000 + request.EmbarkY));
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
