using HumanFortress.Core.Commands;
using HumanFortress.Core.Content.Registry;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Runtime;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Owns the lifecycle and composition of one active simulation session.
/// </summary>
internal sealed class SimulationRuntimeHost
{
    private readonly World _world;
    private readonly TickScheduler _tickScheduler;
    private readonly CommandQueue _commandQueue;
    private readonly DiffLog _diffLog;
    private readonly ItemsDiffLog _itemsDiffLog;
    private readonly CreaturesDiffLog _creaturesDiffLog;
    private readonly NavigationManager _navigation;
    private readonly string _baseDir;
    private readonly SimulationRuntimeContext _context;
    private readonly SimulationRuntimeHostCore _core;

    private SimulationRuntimeSystems? _systems;

    public SimulationRuntimeHost(
        World world,
        TickScheduler tickScheduler,
        CommandQueue commandQueue,
        IEventBus eventBus,
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        NavigationManager navigation,
        string baseDir)
    {
        _world = world;
        _tickScheduler = tickScheduler;
        _commandQueue = commandQueue;
        _diffLog = diffLog;
        _itemsDiffLog = itemsDiffLog;
        _creaturesDiffLog = new CreaturesDiffLog();
        _navigation = navigation;
        _baseDir = baseDir;
        _context = new SimulationRuntimeContext(
            diffLog,
            itemsDiffLog,
            _creaturesDiffLog,
            world,
            eventBus,
            Logger.Log,
            ContentRegistry.Instance.Recipes,
            ContentRegistry.Instance.Constructions);
        _core = new SimulationRuntimeHostCore(
            world,
            tickScheduler,
            commandQueue,
            _context,
            diffLog,
            itemsDiffLog,
            _creaturesDiffLog,
            navigation);
    }

    public World World => _world;
    public NavigationManager Navigation => _navigation;
    public SimulationRuntimeSystems? Systems => _systems;
    public bool IsRunning => _core.IsRunning;

    public void Start(bool enqueueAutoDig)
    {
        _systems = null;
        _systems = _core.Start(
            () => SimulationRuntimeSystems.Create(
                _world,
                _diffLog,
                _itemsDiffLog,
                _navigation,
                _baseDir),
            systems => _context.SetProfessionWeightHandler(systems.ProfessionAssignments.SetWeight),
            systems =>
            {
                SimulationInitialWorkerSpawner.SpawnIfNeeded(_world);
                systems.ProfessionAssignments.Initialize(_world.Creatures.GetAllInstances());

                if (!enqueueAutoDig)
                    return;

                try
                {
                    SimulationAutoDigSeeder.EnqueueIfPossible(_world, _commandQueue, _tickScheduler.CurrentTick);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[AUTO-DIG] ERROR: {ex.Message}");
                }
            });
    }

    public void Stop()
    {
        _core.Stop();
    }
}
