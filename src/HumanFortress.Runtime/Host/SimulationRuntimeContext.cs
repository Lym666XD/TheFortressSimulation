using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Runtime.Commands;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Host;

/// <summary>
/// Mutable per-session simulation clock/read context owned by the runtime tick pipeline.
/// </summary>
internal sealed class SimulationRuntimeContext :
    IRuntimeCommandClockContext,
    ISimulationContext
{
    private readonly DiffLog _diffLog;
    private readonly World _world;
    private readonly IEventBus _eventBus;
    private ulong _currentTick;

    internal SimulationRuntimeContext(
        DiffLog diffLog,
        World world,
        IEventBus eventBus)
    {
        _diffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    DiffLog ISimulationContext.DiffLog => _diffLog;
    ulong ISimulationContext.CurrentTick => _currentTick;
    IWorldReader ISimulationContext.World => _world;
    IEventBus ISimulationContext.EventBus => _eventBus;

    void IRuntimeCommandClockContext.SetCurrentTick(ulong tick)
    {
        _currentTick = tick;
    }
}
