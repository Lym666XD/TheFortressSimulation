namespace HumanFortress.App.Session;

internal sealed record FortressSessionInitializationResult(
    bool HasWorld,
    bool HasFortressMap,
    EmbarkSiteSummary? EmbarkSite,
    bool UsedFallbackWorld);
