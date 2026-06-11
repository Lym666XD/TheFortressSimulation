using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

/// <summary>
/// Owns scheduler/pipeline lifecycle for one active simulation session.
/// </summary>
public sealed class SimulationRuntimeHostCore
{
    private readonly World _world;
    private readonly TickScheduler _tickScheduler;
    private readonly CommandQueue _commandQueue;
    private readonly IRuntimeCommandContext _context;
    private readonly DiffLog _diffLog;
    private readonly ItemsDiffLog _itemsDiffLog;
    private readonly CreaturesDiffLog _creaturesDiffLog;
    private readonly NavigationManager? _navigation;

    private SimulationTickPipeline? _pipeline;

    public SimulationRuntimeHostCore(
        World world,
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IRuntimeCommandContext context,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        CreaturesDiffLog creaturesDiffLog,
        NavigationManager? navigation)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _tickScheduler = tickScheduler ?? throw new ArgumentNullException(nameof(tickScheduler));
        _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _diffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        _itemsDiffLog = itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog));
        _creaturesDiffLog = creaturesDiffLog ?? throw new ArgumentNullException(nameof(creaturesDiffLog));
        _navigation = navigation;
    }

    public bool IsRunning => _tickScheduler.IsRunning;

    public TSystems Configure<TSystems>(
        Func<TSystems> createSystems,
        Action<TSystems>? afterSystemsRegistered = null,
        Action<TSystems>? afterPipelineAttached = null)
        where TSystems : class, IRuntimeTickSystems
    {
        ArgumentNullException.ThrowIfNull(createSystems);

        StopScheduler();
        DetachPipeline();
        _tickScheduler.ClearSystems();

        var systems = createSystems()
            ?? throw new InvalidOperationException("Runtime systems factory returned null.");

        systems.RegisterWith(_tickScheduler);
        afterSystemsRegistered?.Invoke(systems);

        _pipeline = new SimulationTickPipeline(
            _world,
            _commandQueue,
            _context,
            _diffLog,
            _itemsDiffLog,
            _creaturesDiffLog,
            _navigation);
        _pipeline.AttachTo(_tickScheduler);

        afterPipelineAttached?.Invoke(systems);
        return systems;
    }

    public TSystems Start<TSystems>(
        Func<TSystems> createSystems,
        Action<TSystems>? afterSystemsRegistered = null,
        Action<TSystems>? afterPipelineAttached = null)
        where TSystems : class, IRuntimeTickSystems
    {
        var systems = Configure(createSystems, afterSystemsRegistered, afterPipelineAttached);
        _tickScheduler.Start();
        return systems;
    }

    public void Stop()
    {
        StopScheduler();
        DetachPipeline();
    }

    private void StopScheduler()
    {
        _tickScheduler.Stop();
    }

    private void DetachPipeline()
    {
        if (_pipeline == null)
            return;

        _pipeline.DetachFrom(_tickScheduler);
        _pipeline = null;
    }
}
