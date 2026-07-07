namespace HumanFortress.App.Session;

internal sealed partial class FortressSessionInitializer
{
    private FortressSessionInitializationResult FallbackToRuntimeWorld()
    {
        return new FortressSessionInitializationResult(
            HasWorld: _runtime.GetWorldAvailabilityData().HasWorld,
            HasFortressMap: false,
            EmbarkSite: null,
            UsedFallbackWorld: true);
    }
}
