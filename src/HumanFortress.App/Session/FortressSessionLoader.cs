using HumanFortress.App.Input;
using HumanFortress.App.UI;

namespace HumanFortress.App.Session;

internal sealed class FortressSessionLoader
{
    private readonly FortressSessionRuntimePorts _runtime;
    private readonly FortressSessionContext _session;
    private readonly UiStore _ui;
    private readonly Func<ulong> _uiTickProvider;
    private readonly InputBindingsService _bindings;
    private readonly OrdersRegistryService _ordersRegistry;
    private readonly string _baseDir;

    internal FortressSessionLoader(
        FortressSessionRuntimePorts runtime,
        FortressSessionContext session,
        UiStore ui,
        Func<ulong> uiTickProvider,
        InputBindingsService bindings,
        OrdersRegistryService ordersRegistry,
        string baseDir)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _uiTickProvider = uiTickProvider ?? throw new ArgumentNullException(nameof(uiTickProvider));
        _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        _ordersRegistry = ordersRegistry ?? throw new ArgumentNullException(nameof(ordersRegistry));
        _baseDir = string.IsNullOrWhiteSpace(baseDir) ? throw new ArgumentException("Base directory is required.", nameof(baseDir)) : baseDir;
    }

    internal FortressSessionLoadResult Load(int currentZ)
    {
        try
        {
            var initialization = new FortressSessionInitializer(_runtime, _session).Initialize();

            if (!initialization.HasWorld || initialization.UsedFallbackWorld)
                return FromInitialization(initialization);

            var bindings = FortressSessionRuntimeBootstrapper.Configure(
                _runtime,
                _ui,
                _uiTickProvider,
                _session.AutoDig,
                currentZ,
                _bindings,
                _ordersRegistry,
                _baseDir);

            return new FortressSessionLoadResult(
                true,
                initialization.HasFortressMap,
                bindings.NavigationOverlay,
                bindings.UiServices,
                initialization.EmbarkSite,
                UsedFallbackWorld: false);
        }
        catch (Exception ex)
        {
            Logger.Error("UI.GenerateFortressMap", $"[GenerateFortressMap] ERROR: {ex.Message}", ex);

            Logger.Log("[GenerateFortressMap] Using runtime World despite error");
            return new FortressSessionLoadResult(
                _runtime.GetWorldAvailabilityData().HasWorld,
                false,
                null,
                null,
                null,
                UsedFallbackWorld: true);
        }
    }

    private static FortressSessionLoadResult FromInitialization(FortressSessionInitializationResult initialization)
    {
        return new FortressSessionLoadResult(
            initialization.HasWorld,
            initialization.HasFortressMap,
            null,
            null,
            initialization.EmbarkSite,
            initialization.UsedFallbackWorld);
    }
}
