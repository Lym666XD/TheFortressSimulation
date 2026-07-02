using HumanFortress.Navigation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

/// <summary>
/// Creates fresh simulation sessions while keeping App-specific host/content
/// composition behind callbacks.
/// </summary>
internal sealed class SimulationRuntimeSessionFactory<THost>
    where THost : class
{
    private readonly RuntimeSessionServices _services;
    private readonly Action<World> _loadContent;
    private readonly Func<NavigationTuning?>? _getNavigationTuning;
    private readonly Func<World, NavigationManager, THost> _createHost;

    internal SimulationRuntimeSessionFactory(
        RuntimeSessionServices services,
        Action<World> loadContent,
        Func<World, NavigationManager, THost> createHost,
        Func<NavigationTuning?>? getNavigationTuning = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _loadContent = loadContent ?? throw new ArgumentNullException(nameof(loadContent));
        _createHost = createHost ?? throw new ArgumentNullException(nameof(createHost));
        _getNavigationTuning = getNavigationTuning;
    }

    internal SimulationRuntimeSession<THost> CreateNew(int sizeInChunks, int maxZ)
    {
        if (sizeInChunks <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeInChunks), "Session size must be positive.");
        if (maxZ <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxZ), "Session max Z must be positive.");

        _services.ResetForNewSession();

        var world = new World(sizeInChunks, maxZ);
        _loadContent(world);
        var navigation = SimulationNavigationFactory.Create(
            world,
            rebuildAll: false,
            _getNavigationTuning?.Invoke());

        var host = _createHost(world, navigation)
            ?? throw new InvalidOperationException("Runtime host factory returned null.");

        return new SimulationRuntimeSession<THost>(world, navigation, host);
    }
}
