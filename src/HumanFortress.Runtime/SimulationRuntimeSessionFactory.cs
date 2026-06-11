using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

/// <summary>
/// Creates fresh simulation sessions while keeping App-specific host/content
/// composition behind callbacks.
/// </summary>
public sealed class SimulationRuntimeSessionFactory<THost>
    where THost : class
{
    private readonly TickScheduler _tickScheduler;
    private readonly CommandQueue _commandQueue;
    private readonly DiffLog _diffLog;
    private readonly ItemsDiffLog _itemsDiffLog;
    private readonly Action<World> _loadContent;
    private readonly Func<World, NavigationManager, THost> _createHost;

    public SimulationRuntimeSessionFactory(
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        Action<World> loadContent,
        Func<World, NavigationManager, THost> createHost)
    {
        _tickScheduler = tickScheduler ?? throw new ArgumentNullException(nameof(tickScheduler));
        _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        _diffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        _itemsDiffLog = itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog));
        _loadContent = loadContent ?? throw new ArgumentNullException(nameof(loadContent));
        _createHost = createHost ?? throw new ArgumentNullException(nameof(createHost));
    }

    public SimulationRuntimeSession<THost> CreateNew(int sizeInChunks, int maxZ)
    {
        if (sizeInChunks <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeInChunks), "Session size must be positive.");
        if (maxZ <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxZ), "Session max Z must be positive.");

        _tickScheduler.ResetForNewSession();
        _commandQueue.Clear();
        _diffLog.Clear();
        _itemsDiffLog.Clear();

        var world = new World(sizeInChunks, maxZ);
        var navigation = SimulationNavigationFactory.Create(world, rebuildAll: false);
        _loadContent(world);

        var host = _createHost(world, navigation)
            ?? throw new InvalidOperationException("Runtime host factory returned null.");

        return new SimulationRuntimeSession<THost>(world, navigation, host);
    }
}
