using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation.Implementation;
using HumanFortress.Runtime.Commands;
using HumanFortress.Runtime.Diff;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Host;

/// <summary>
/// Owns scheduler/pipeline lifecycle for one active simulation session.
/// </summary>
internal sealed partial class SimulationRuntimeHostCore
{
    private readonly World _world;
    private readonly TickScheduler _tickScheduler;
    private readonly CommandQueue _commandQueue;
    private readonly IRuntimeCommandClockContext _clockContext;
    private readonly ISimulationContext _commandContext;
    private readonly DiffLog _diffLog;
    private readonly RuntimeMutationDiffLogs _mutationDiffs;
    private readonly IConstructionCatalog _constructions;
    private readonly NavigationManager? _navigation;
    private readonly IRuntimeGeologyCatalog? _geology;

    private SimulationTickPipeline? _pipeline;

    internal SimulationRuntimeHostCore(
        World world,
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IRuntimeCommandClockContext clockContext,
        ISimulationContext commandContext,
        DiffLog diffLog,
        RuntimeMutationDiffLogs mutationDiffs,
        IConstructionCatalog constructions,
        NavigationManager? navigation,
        IRuntimeGeologyCatalog? geology = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _tickScheduler = tickScheduler ?? throw new ArgumentNullException(nameof(tickScheduler));
        _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        _clockContext = clockContext ?? throw new ArgumentNullException(nameof(clockContext));
        _commandContext = commandContext ?? throw new ArgumentNullException(nameof(commandContext));
        _diffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        _mutationDiffs = mutationDiffs ?? throw new ArgumentNullException(nameof(mutationDiffs));
        _constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
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
            _mutationDiffs,
            _constructions,
            _navigation,
            _geology);
        _pipeline.AttachTo(_tickScheduler);

        afterPipelineAttached?.Invoke(systems);
        return systems;
    }

}
