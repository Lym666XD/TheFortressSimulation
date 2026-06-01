using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
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
    private readonly NavigationManager _navigation;
    private readonly string _baseDir;
    private readonly SimulationRuntimeContext _context;

    private SimulationTickPipeline? _pipeline;
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
        _navigation = navigation;
        _baseDir = baseDir;
        _context = new SimulationRuntimeContext(diffLog, world, eventBus);
    }

    public World World => _world;
    public NavigationManager Navigation => _navigation;
    public SimulationRuntimeSystems? Systems => _systems;
    public bool IsRunning => _tickScheduler.IsRunning;

    public void Start(bool enqueueAutoDig)
    {
        if (_tickScheduler.IsRunning)
        {
            _tickScheduler.Stop();
        }

        DetachPipeline();
        _tickScheduler.ClearSystems();

        _systems = SimulationRuntimeSystems.Create(
            _world,
            _diffLog,
            _itemsDiffLog,
            _navigation,
            _baseDir);
        _systems.RegisterWith(_tickScheduler);

        _pipeline = new SimulationTickPipeline(
            _world,
            _commandQueue,
            _context,
            _diffLog,
            _itemsDiffLog,
            _navigation);
        _pipeline.AttachTo(_tickScheduler);

        SimulationInitialWorkerSpawner.SpawnIfNeeded(_world);
        _systems.ProfessionAssignments.Initialize(_world.Creatures.GetAllInstances());

        if (enqueueAutoDig)
        {
            try
            {
                SimulationAutoDigSeeder.EnqueueIfPossible(_world, _commandQueue, _tickScheduler.CurrentTick);
            }
            catch (Exception ex)
            {
                Logger.Log($"[AUTO-DIG] ERROR: {ex.Message}");
            }
        }

        _tickScheduler.Start();
    }

    public void Stop()
    {
        if (_tickScheduler.IsRunning)
        {
            _tickScheduler.Stop();
        }

        DetachPipeline();
    }

    private void DetachPipeline()
    {
        if (_pipeline == null)
            return;

        _pipeline.DetachFrom(_tickScheduler);
        _pipeline = null;
    }
}
