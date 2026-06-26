using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

/// <summary>
/// Owns the lifecycle and composition hooks for one active simulation session.
/// </summary>
internal sealed partial class SimulationRuntimeHost<TSystems>
    where TSystems : class, IRuntimeTickSystems
{
    private readonly World _world;
    private readonly TickScheduler _tickScheduler;
    private readonly CommandQueue _commandQueue;
    private readonly DiffLog _diffLog;
    private readonly ItemsDiffLog _itemsDiffLog;
    private readonly CreaturesDiffLog _creaturesDiffLog;
    private readonly NavigationManager _navigation;
    private readonly NavigationTuning _navigationTuning;
    private readonly IRecipeCatalog _recipes;
    private readonly IConstructionCatalog _constructions;
    private readonly IRuntimeGeologyCatalog _geology;
    private readonly SimulationCommandExecutionContext _commandContext;
    private readonly SimulationRuntimeHostCore _core;
    private readonly Func<TSystems> _createSystems;
    private readonly Action<IRuntimeProfessionCommandBindings, TSystems>? _afterSystemsRegistered;

    private TSystems? _systems;

    internal SimulationRuntimeHost(
        World world,
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IEventBus eventBus,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
        Func<TSystems> createSystems,
        Action<IRuntimeProfessionCommandBindings, TSystems>? afterSystemsRegistered = null,
        Action<string>? log = null,
        IRecipeCatalog? recipes = null,
        IConstructionCatalog? constructions = null,
        IRuntimeGeologyCatalog? geology = null,
        NavigationTuning? navigationTuning = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _tickScheduler = tickScheduler ?? throw new ArgumentNullException(nameof(tickScheduler));
        _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        _diffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        _itemsDiffLog = itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog));
        _creaturesDiffLog = new CreaturesDiffLog();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _navigationTuning = navigationTuning ?? NavigationTuning.Default;
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _constructions = constructions ?? throw new ArgumentNullException(nameof(constructions));
        _geology = geology ?? throw new ArgumentNullException(nameof(geology));
        _createSystems = createSystems ?? throw new ArgumentNullException(nameof(createSystems));
        _afterSystemsRegistered = afterSystemsRegistered;

        var context = new SimulationRuntimeContext(
            diffLog,
            world,
            eventBus);
        _commandContext = new SimulationCommandExecutionContext(
            context,
            context,
            world,
            itemsDiffLog,
            _creaturesDiffLog,
            _recipes,
            _constructions,
            log);
        _core = new SimulationRuntimeHostCore(
            world,
            tickScheduler,
            commandQueue,
            _commandContext,
            _commandContext,
            diffLog,
            itemsDiffLog,
            _creaturesDiffLog,
            navigation,
            _geology);
    }

}
