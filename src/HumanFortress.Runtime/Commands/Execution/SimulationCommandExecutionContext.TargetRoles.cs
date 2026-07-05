namespace HumanFortress.Runtime.Commands;

internal sealed partial class SimulationCommandExecutionContext :
    IRuntimeProfessionCommandTargetContext,
    IRuntimeItemSpawnCommandTargetContext,
    IRuntimeCreatureSpawnCommandTargetContext,
    IRuntimeOrderCommandTargetContext,
    IRuntimeZoneCommandTargetContext,
    IRuntimeWorkshopCommandTargetContext,
    IRuntimeStockpileCommandTargetContext
{
    IProfessionAssignmentCommandTarget IRuntimeProfessionCommandTargetContext.Professions => _commandTargets.Professions;
    IItemSpawnCommandTarget IRuntimeItemSpawnCommandTargetContext.Items => _commandTargets.Items;
    ICreatureSpawnCommandTarget IRuntimeCreatureSpawnCommandTargetContext.Creatures => _commandTargets.Creatures;
    IOrderCommandTarget IRuntimeOrderCommandTargetContext.Orders => _commandTargets.Orders;
    IZoneCommandTarget IRuntimeZoneCommandTargetContext.Zones => _commandTargets.Zones;
    IWorkshopQueueCommandTarget IRuntimeWorkshopCommandTargetContext.Workshops => _commandTargets.Workshops;
    IStockpileCommandTarget IRuntimeStockpileCommandTargetContext.Stockpiles => _commandTargets.Stockpiles;
}
