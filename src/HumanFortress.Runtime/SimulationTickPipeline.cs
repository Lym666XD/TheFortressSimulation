using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Simulation;
using HumanFortress.Core.Time;
using HumanFortress.Navigation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Diff;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

/// <summary>
/// Owns the pre/post tick barriers for one active simulation session.
/// </summary>
internal sealed partial class SimulationTickPipeline
{
    private readonly World _world;
    private readonly SimulationCommandStage _commandStage;
    private readonly DiffLog _diffLog;
    private readonly ItemsDiffLog _itemsDiffLog;
    private readonly CreaturesDiffLog _creaturesDiffLog;
    private readonly NavigationManager? _navigation;
    private readonly IRuntimeGeologyCatalog? _geology;

    internal SimulationTickPipeline(
        World world,
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
        _commandStage = new SimulationCommandStage(commandQueue, clockContext, commandContext);
        _diffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        _itemsDiffLog = itemsDiffLog ?? throw new ArgumentNullException(nameof(itemsDiffLog));
        _creaturesDiffLog = creaturesDiffLog ?? throw new ArgumentNullException(nameof(creaturesDiffLog));
        _navigation = navigation;
        _geology = geology;
    }

    internal void AttachTo(TickScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        scheduler.PreTick += ExecutePreTick;
        scheduler.PostTick += ExecutePostTick;
    }

    internal void DetachFrom(TickScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        scheduler.PreTick -= ExecutePreTick;
        scheduler.PostTick -= ExecutePostTick;
    }

    private void ExecutePreTick(ulong tick)
    {
        _commandStage.Execute(tick);
    }
}
