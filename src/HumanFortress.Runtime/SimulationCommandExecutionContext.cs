using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal sealed class SimulationCommandExecutionContext :
    IRuntimeCommandClockContext,
    ISimulationContext,
    IRuntimeProfessionCommandTargetContext,
    IRuntimeItemSpawnCommandTargetContext,
    IRuntimeCreatureSpawnCommandTargetContext,
    IRuntimeOrderCommandTargetContext,
    IRuntimeZoneCommandTargetContext,
    IRuntimeWorkshopCommandTargetContext,
    IRuntimeStockpileCommandTargetContext
{
    private readonly IRuntimeCommandClockContext _clockContext;
    private readonly ISimulationContext _simulationContext;
    private readonly SimulationRuntimeCommandTargets _commandTargets;

    internal SimulationCommandExecutionContext(
        IRuntimeCommandClockContext clockContext,
        ISimulationContext simulationContext,
        World world,
        RuntimeMutationDiffLogs mutationDiffs,
        IRecipeCatalog recipes,
        FortressRuntimeStockpilePresetCatalog? stockpilePresets = null,
        Action<string>? log = null)
    {
        _clockContext = clockContext ?? throw new ArgumentNullException(nameof(clockContext));
        _simulationContext = simulationContext ?? throw new ArgumentNullException(nameof(simulationContext));
        _commandTargets = new SimulationRuntimeCommandTargets(
            world,
            mutationDiffs,
            recipes,
            stockpilePresets,
            log);
    }

    internal IRuntimeProfessionCommandBindings ProfessionCommandBindings => _commandTargets;

    DiffLog ISimulationContext.DiffLog => _simulationContext.DiffLog;
    ulong ISimulationContext.CurrentTick => _simulationContext.CurrentTick;
    IWorldReader ISimulationContext.World => _simulationContext.World;
    IEventBus ISimulationContext.EventBus => _simulationContext.EventBus;
    IProfessionAssignmentCommandTarget IRuntimeProfessionCommandTargetContext.Professions => _commandTargets.Professions;
    IItemSpawnCommandTarget IRuntimeItemSpawnCommandTargetContext.Items => _commandTargets.Items;
    ICreatureSpawnCommandTarget IRuntimeCreatureSpawnCommandTargetContext.Creatures => _commandTargets.Creatures;
    IOrderCommandTarget IRuntimeOrderCommandTargetContext.Orders => _commandTargets.Orders;
    IZoneCommandTarget IRuntimeZoneCommandTargetContext.Zones => _commandTargets.Zones;
    IWorkshopQueueCommandTarget IRuntimeWorkshopCommandTargetContext.Workshops => _commandTargets.Workshops;
    IStockpileCommandTarget IRuntimeStockpileCommandTargetContext.Stockpiles => _commandTargets.Stockpiles;

    void IRuntimeCommandClockContext.SetCurrentTick(ulong tick)
    {
        _clockContext.SetCurrentTick(tick);
    }
}
