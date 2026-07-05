using HumanFortress.Core.Commands;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class SimulationCommandExecutionContext :
    ISimulationContext
{
    DiffLog ISimulationContext.DiffLog => _simulationContext.DiffLog;
    ulong ISimulationContext.CurrentTick => _simulationContext.CurrentTick;
    IWorldReader ISimulationContext.World => _simulationContext.World;
    IEventBus ISimulationContext.EventBus => _simulationContext.EventBus;
}
