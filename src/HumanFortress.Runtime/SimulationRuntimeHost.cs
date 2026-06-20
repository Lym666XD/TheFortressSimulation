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
public sealed class SimulationRuntimeHost<TSystems>
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
    private readonly SimulationRuntimeContext _context;
    private readonly SimulationRuntimeHostCore _core;
    private readonly Func<TSystems> _createSystems;
    private readonly Action<SimulationRuntimeContext, TSystems>? _afterSystemsRegistered;

    private TSystems? _systems;

    public SimulationRuntimeHost(
        World world,
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IEventBus eventBus,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
        Func<TSystems> createSystems,
        Action<SimulationRuntimeContext, TSystems>? afterSystemsRegistered = null,
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

        _context = new SimulationRuntimeContext(
            diffLog,
            itemsDiffLog,
            _creaturesDiffLog,
            world,
            eventBus,
            _recipes,
            _constructions,
            log);
        _core = new SimulationRuntimeHostCore(
            world,
            tickScheduler,
            commandQueue,
            _context,
            diffLog,
            itemsDiffLog,
            _creaturesDiffLog,
            navigation,
            _geology);
    }

    public World World => _world;
    public NavigationManager Navigation => _navigation;
    public NavigationTuning NavigationTuning => _navigationTuning;
    public IRecipeCatalog Recipes => _recipes;
    public IConstructionCatalog Constructions => _constructions;
    public IRuntimeGeologyCatalog Geology => _geology;
    public TSystems? Systems => _systems;
    public bool IsRunning => _core.IsRunning;

    public void Start(Action<TSystems>? afterPipelineAttached = null)
    {
        _systems = null;
        _systems = _core.Start(
            _createSystems,
            systems => _afterSystemsRegistered?.Invoke(_context, systems),
            afterPipelineAttached);
    }

    public void Stop()
    {
        _core.Stop();
    }
}
