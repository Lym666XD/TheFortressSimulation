using HumanFortress.Contracts.Runtime;

namespace HumanFortress.App.Session;

internal sealed partial class FortressSessionInitializer
{
    private readonly FortressSessionRuntimePorts _runtime;
    private readonly FortressSessionContext _session;

    internal FortressSessionInitializer(FortressSessionRuntimePorts runtime, FortressSessionContext session)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    internal FortressSessionInitializationResult Initialize()
    {
        try
        {
            int fortressSize = _session.FortressSize;
            var embarkLocation = _session.EmbarkLocation;

            Logger.Log("[GenerateFortressMap] Starting fortress generation");
            Logger.Log($"[GenerateFortressMap] FortressSize: {fortressSize}, EmbarkLocation: {embarkLocation}");

            if (!TryGetEmbarkTileSnapshot(embarkLocation, out var worldTile))
                return FallbackToRuntimeWorld();

            Logger.Log("[GenerateFortressMap] Generating and filling runtime world");
            var generation = _runtime.GenerateAndFillFortressWorld(
                CreateGenerationRequest(fortressSize, worldTile, embarkLocation));

            if (generation.Status == RuntimeFortressGenerationStatus.MissingGenerationContent)
            {
                Logger.Log("[GenerateFortressMap] ERROR: Runtime generation content is not available");
                return FallbackToRuntimeWorld();
            }

            if (generation.Status == RuntimeFortressGenerationStatus.MissingRuntimeWorld)
            {
                Logger.Log("[GenerateFortressMap] ERROR: runtime World is null");
                return new FortressSessionInitializationResult(
                    HasWorld: false,
                    HasFortressMap: true,
                    EmbarkSite: CreateEmbarkSiteSummary(embarkLocation, worldTile),
                    UsedFallbackWorld: false);
            }

            Logger.Log($"[GenerateFortressMap] Runtime world filled from fortress map: {generation.FortressMapSize}x{generation.FortressMapSize} chunks");

            return new FortressSessionInitializationResult(
                HasWorld: true,
                HasFortressMap: true,
                EmbarkSite: CreateEmbarkSiteSummary(embarkLocation, worldTile),
                UsedFallbackWorld: false);
        }
        catch (Exception ex)
        {
            Logger.Error("UI.GenerateFortressMap", $"[GenerateFortressMap] ERROR: {ex.Message}", ex);

            Logger.Log("[GenerateFortressMap] Using runtime World despite error");
            return FallbackToRuntimeWorld();
        }
    }

}
