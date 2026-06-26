using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
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
internal sealed partial class SimulationRuntimeHostCore
{
    private readonly World _world;
    private readonly TickScheduler _tickScheduler;
    private readonly CommandQueue _commandQueue;
    private readonly IRuntimeCommandClockContext _clockContext;
    private readonly IRuntimeCommandExecutionContext _commandContext;
    private readonly DiffLog _diffLog;
    private readonly ItemsDiffLog _itemsDiffLog;
    private readonly CreaturesDiffLog _creaturesDiffLog;
    private readonly NavigationManager? _navigation;
    private readonly IRuntimeGeologyCatalog? _geology;

    private SimulationTickPipeline? _pipeline;

    internal SimulationRuntimeHostCore(
        World world,
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IRuntimeCommandClockContext clockContext,
        IRuntimeCommandExecutionContext commandContext,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        CreaturesDiffLog creaturesDiffLog,
        NavigationManager? navigation,
        IRuntimeGeologyCatalog? geology = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _tickScheduler = tickScheduler ?? throw new ArgumentNullException(nameof(tickScheduler));
        _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        _clockContext = clockContext ?? throw new ArgumentNullException(nameof(clockContext));
        _commandContext = commandContext ?? throw new ArgumentNullException(nameof(commandContext));
        _diffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        _itemsDiffLog = itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog));
        _creaturesDiffLog = creaturesDiffLog ?? throw new ArgumentNullException(nameof(creaturesDiffLog));
        _navigation = navigation;
        _geology = geology;
    }

    internal bool IsRunning => _tickScheduler.IsRunning;

    internal TSystems Configure<TSystems>(
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
            _clockContext,
            _commandContext,
            _diffLog,
            _itemsDiffLog,
            _creaturesDiffLog,
            _navigation,
            _geology);
        _pipeline.AttachTo(_tickScheduler);

        afterPipelineAttached?.Invoke(systems);
        return systems;
    }

}
