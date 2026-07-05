using HumanFortress.App.Input;
using HumanFortress.App.UI;

namespace HumanFortress.App.Session;

internal sealed class FortressSessionLoadCoordinator
{
    private readonly FortressSessionRuntimePorts _runtime;
    private readonly FortressSessionContext _session;
    private readonly FortressLoadedSessionState _loadedSession;
    private readonly UiStore _ui;
    private readonly Func<ulong> _uiTickProvider;
    private readonly string _baseDir;

    internal FortressSessionLoadCoordinator(
        FortressSessionRuntimePorts runtime,
        FortressSessionContext session,
        FortressLoadedSessionState loadedSession,
        UiStore ui,
        Func<ulong> uiTickProvider,
        string baseDir)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _loadedSession = loadedSession ?? throw new ArgumentNullException(nameof(loadedSession));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _uiTickProvider = uiTickProvider ?? throw new ArgumentNullException(nameof(uiTickProvider));
        _baseDir = string.IsNullOrWhiteSpace(baseDir)
            ? throw new ArgumentException("Base directory is required.", nameof(baseDir))
            : baseDir;
    }

    internal void Load(int currentZ)
    {
        var loaded = new FortressSessionLoader(
            _runtime,
            _session,
            _ui,
            _uiTickProvider,
            InputBindingsService.Instance,
            OrdersRegistryService.Instance,
            _baseDir).Load(currentZ);

        _loadedSession.Apply(loaded);

        if (!loaded.HasWorld || loaded.UsedFallbackWorld)
            return;

        Logger.Log($"[GenerateFortressMap] SUCCESS: Generated fortress map: {_session.FortressSize}x{_session.FortressSize} chunks at {_session.EmbarkLocation}");
        if (loaded.EmbarkSite.HasValue)
        {
            var embarkSite = loaded.EmbarkSite.Value;
            Logger.Log($"[GenerateFortressMap] Biome: {embarkSite.BiomeName}, Elevation: {embarkSite.Elevation:F2}");
        }
    }
}
