using HumanFortress.App.Input;
using HumanFortress.App.UI;

namespace HumanFortress.App.Runtime;

internal sealed class FortressSessionLoader
{
    private readonly FortressRuntimeAccess _runtime;
    private readonly FortressSessionContext _session;
    private readonly UiStore _ui;
    private readonly Func<ulong> _uiTickProvider;
    private readonly InputBindingsService _bindings;
    private readonly OrdersRegistryService _ordersRegistry;
    private readonly string _baseDir;

    public FortressSessionLoader(
        FortressRuntimeAccess runtime,
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

    public FortressSessionLoadResult Load(int currentZ)
    {
        try
        {
            var initialization = new FortressSessionInitializer(_runtime, _session).Initialize();
            var world = initialization.World;

            if (world == null || initialization.UsedFallbackWorld)
                return FromInitialization(initialization);

            var bindings = FortressSessionRuntimeBootstrapper.Configure(
                world,
                initialization.NavigationManager,
                _runtime,
                _ui,
                _uiTickProvider,
                _session.AutoDig,
                currentZ,
                _bindings,
                _ordersRegistry,
                _baseDir);

            return new FortressSessionLoadResult(
                world,
                initialization.FortressMap,
                initialization.SnapshotBuilder,
                initialization.NavigationManager,
                bindings.NavigationOverlay,
                bindings.OverlayFromSnapshot,
                bindings.UiServices,
                initialization.WorldTile,
                UsedFallbackWorld: false);
        }
        catch (Exception ex)
        {
            Logger.Error("UI.GenerateFortressMap", $"[GenerateFortressMap] ERROR: {ex.Message}", ex);

            Logger.Log("[GenerateFortressMap] Using runtime World despite error");
            return new FortressSessionLoadResult(
                _runtime.World,
                null,
                null,
                null,
                null,
                false,
                null,
                null,
                UsedFallbackWorld: true);
        }
    }

    private static FortressSessionLoadResult FromInitialization(FortressSessionInitializationResult initialization)
    {
        return new FortressSessionLoadResult(
            initialization.World,
            initialization.FortressMap,
            initialization.SnapshotBuilder,
            initialization.NavigationManager,
            null,
            false,
            null,
            initialization.WorldTile,
            initialization.UsedFallbackWorld);
    }
}
