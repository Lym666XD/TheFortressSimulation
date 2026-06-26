using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime;

internal interface IRuntimeCommandExecutionContext :
    ISimulationContext,
    IRuntimeProfessionCommandTargetContext,
    IRuntimeItemSpawnCommandTargetContext,
    IRuntimeCreatureSpawnCommandTargetContext,
    IRuntimeOrderCommandTargetContext,
    IRuntimeZoneCommandTargetContext,
    IRuntimeWorkshopCommandTargetContext,
    IRuntimeStockpileCommandTargetContext
{
}

internal interface IRuntimeProfessionCommandTargetContext
{
    IProfessionAssignmentCommandTarget Professions { get; }
}

internal interface IRuntimeItemSpawnCommandTargetContext
{
    IItemSpawnCommandTarget Items { get; }
}

internal interface IRuntimeCreatureSpawnCommandTargetContext
{
    ICreatureSpawnCommandTarget Creatures { get; }
}

internal interface IRuntimeOrderCommandTargetContext
{
    IOrderCommandTarget Orders { get; }
}

internal interface IRuntimeZoneCommandTargetContext
{
    IZoneCommandTarget Zones { get; }
}

internal interface IRuntimeWorkshopCommandTargetContext
{
    IWorkshopQueueCommandTarget Workshops { get; }
}

internal interface IRuntimeStockpileCommandTargetContext
{
    IStockpileCommandTarget Stockpiles { get; }
}
