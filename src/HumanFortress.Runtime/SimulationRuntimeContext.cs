using HumanFortress.Core.Commands;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Events;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime;

/// <summary>
/// Mutable per-session command context owned by the runtime tick pipeline.
/// </summary>
public sealed class SimulationRuntimeContext :
    IRuntimeCommandContext,
    IProfessionAssignmentCommandTarget,
    IItemSpawnCommandTarget,
    ICreatureSpawnCommandTarget,
    IOrderCommandTarget,
    IZoneCommandTarget,
    IWorkshopQueueCommandTarget,
    IStockpileCommandTarget
{
    private readonly DiffLog _diffLog;
    private readonly World _world;
    private readonly IEventBus _eventBus;
    private readonly ItemSpawnCommandTarget _itemSpawnCommands;
    private readonly CreatureSpawnCommandTarget _creatureSpawnCommands;
    private readonly OrderCommandTarget _orderCommands;
    private readonly ZoneCommandTarget _zoneCommands;
    private readonly WorkshopQueueCommandTarget _workshopQueueCommands;
    private readonly StockpileCommandTarget _stockpileCommands;
    private Action<Guid, string, int>? _setProfessionWeight;
    private ulong _currentTick;

    public SimulationRuntimeContext(
        DiffLog diffLog,
        ItemsDiffLog itemsDiffLog,
        CreaturesDiffLog creaturesDiffLog,
        World world,
        IEventBus eventBus,
        IRecipeCatalog recipes,
        IConstructionCatalog constructions,
        Action<string>? log = null)
    {
        _diffLog = diffLog ?? throw new ArgumentNullException(nameof(diffLog));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _itemSpawnCommands = new ItemSpawnCommandTarget(world, itemsDiffLog);
        _creatureSpawnCommands = new CreatureSpawnCommandTarget(world, creaturesDiffLog);
        _orderCommands = new OrderCommandTarget(world);
        _zoneCommands = new ZoneCommandTarget(world);
        _workshopQueueCommands = new WorkshopQueueCommandTarget(
            world,
            recipes,
            constructions);
        _stockpileCommands = new StockpileCommandTarget(world, log);
    }

    public DiffLog DiffLog => _diffLog;
    public ulong CurrentTick => _currentTick;
    public IWorldReader World => _world;
    public IEventBus EventBus => _eventBus;

    public void SetCurrentTick(ulong tick)
    {
        _currentTick = tick;
    }

    public void SetProfessionWeightHandler(Action<Guid, string, int>? setProfessionWeight)
    {
        _setProfessionWeight = setProfessionWeight;
    }

    public void SetProfessionWeight(Guid workerId, string professionId, int weight)
    {
        _setProfessionWeight?.Invoke(workerId, professionId, weight);
    }

    public bool AddItemSpawn(string itemId, Point worldPos, int z, int quantity)
    {
        return _itemSpawnCommands.AddItemSpawn(itemId, worldPos, z, quantity);
    }

    public bool AddCreatureSpawn(string creatureId, Point worldPos, int z, string factionId)
    {
        return _creatureSpawnCommands.AddCreatureSpawn(creatureId, worldPos, z, factionId);
    }

    public void EnqueueMiningOrder(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        _orderCommands.EnqueueMiningOrder(worldRect, z, priority, createdTick);
    }

    public void EnqueueAdvancedMiningOrder(Rectangle worldRect, int zMin, int zMax, MiningAction action, int priority, ulong createdTick)
    {
        _orderCommands.EnqueueAdvancedMiningOrder(worldRect, zMin, zMax, action, priority, createdTick);
    }

    public void EnqueueHaulOrder(Rectangle worldRect, int z, int priority, ulong createdTick)
    {
        _orderCommands.EnqueueHaulOrder(worldRect, z, priority, createdTick);
    }

    public void EnqueueConstructionOrder(Rectangle worldRect, int zMin, int zMax, ConstructionShape shape, MaterialFilterSpec filter, int priority, ulong createdTick)
    {
        _orderCommands.EnqueueConstructionOrder(worldRect, zMin, zMax, shape, filter, priority, createdTick);
    }

    public void EnqueueBuildableConstructionOrder(string constructionId, Point anchor, int z, int priority, ulong createdTick)
    {
        _orderCommands.EnqueueBuildableConstructionOrder(constructionId, anchor, z, priority, createdTick);
    }

    public int CreateZone(string defId, string name, Rectangle worldRect, int z, ulong createdTick)
    {
        return _zoneCommands.CreateZone(defId, name, worldRect, z, createdTick);
    }

    public void AddZoneCells(int zoneId, Rectangle worldRect, int z)
    {
        _zoneCommands.AddZoneCells(zoneId, worldRect, z);
    }

    public void RemoveZoneCells(int zoneId, Rectangle worldRect, int z)
    {
        _zoneCommands.RemoveZoneCells(zoneId, worldRect, z);
    }

    public void DeleteZone(int zoneId)
    {
        _zoneCommands.DeleteZone(zoneId);
    }

    public bool AddWorkshopRecipe(Guid workshopGuid, string recipeId, ulong currentTick)
    {
        return _workshopQueueCommands.AddWorkshopRecipe(workshopGuid, recipeId, currentTick);
    }

    public bool RemoveWorkshopQueueEntry(Guid workshopGuid, Guid entryId)
    {
        return _workshopQueueCommands.RemoveWorkshopQueueEntry(workshopGuid, entryId);
    }

    public bool MoveWorkshopQueueEntry(Guid workshopGuid, Guid entryId, int moveOffset)
    {
        return _workshopQueueCommands.MoveWorkshopQueueEntry(workshopGuid, entryId, moveOffset);
    }

    public bool ClearWorkshopQueue(Guid workshopGuid)
    {
        return _workshopQueueCommands.ClearWorkshopQueue(workshopGuid);
    }

    public bool SetWorkshopWorkerSlots(Guid workshopGuid, int workerSlots)
    {
        return _workshopQueueCommands.SetWorkshopWorkerSlots(workshopGuid, workerSlots);
    }

    public bool SetWorkshopAutoStockpile(Guid workshopGuid, bool? value)
    {
        return _workshopQueueCommands.SetWorkshopAutoStockpile(workshopGuid, value);
    }

    public bool SetWorkshopAutoSupply(Guid workshopGuid, bool? value)
    {
        return _workshopQueueCommands.SetWorkshopAutoSupply(workshopGuid, value);
    }

    public bool CreateStockpile(Rectangle worldRect, int z, string presetId, ulong currentTick)
    {
        return _stockpileCommands.CreateStockpile(worldRect, z, presetId, currentTick);
    }
}
