using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.World;

namespace HumanFortress.App.Runtime;

/// <summary>
/// Mutable per-session command context owned by the runtime tick pipeline.
/// </summary>
internal sealed class SimulationRuntimeContext : ISimulationContext
{
    private readonly DiffLog _diffLog;
    private readonly World _world;
    private readonly IEventBus _eventBus;
    private ulong _currentTick;

    public SimulationRuntimeContext(DiffLog diffLog, World world, IEventBus eventBus)
    {
        _diffLog = diffLog;
        _world = world;
        _eventBus = eventBus;
    }

    public DiffLog DiffLog => _diffLog;
    public ulong CurrentTick => _currentTick;
    public IWorldReader World => _world;
    public IEventBus EventBus => _eventBus;

    public void SetCurrentTick(ulong tick)
    {
        _currentTick = tick;
    }
}
