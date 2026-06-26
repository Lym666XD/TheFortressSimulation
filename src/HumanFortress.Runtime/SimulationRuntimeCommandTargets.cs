using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime;

internal sealed class SimulationRuntimeCommandTargets : IRuntimeProfessionCommandBindings
{
    private readonly ProfessionAssignmentCommandTarget _professions;
    private readonly ItemSpawnCommandTarget _items;
    private readonly CreatureSpawnCommandTarget _creatures;
    private readonly OrderCommandTarget _orders;
    private readonly ZoneCommandTarget _zones;
    private readonly WorkshopQueueCommandTarget _workshops;
    private readonly StockpileCommandTarget _stockpiles;

    internal SimulationRuntimeCommandTargets(
        World world,
        ItemsDiffLog itemsDiffLog,
        CreaturesDiffLog creaturesDiffLog,
        IRecipeCatalog recipes,
        IConstructionCatalog constructions,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(itemsDiffLog);
        ArgumentNullException.ThrowIfNull(creaturesDiffLog);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(constructions);

        _professions = new ProfessionAssignmentCommandTarget();
        _items = new ItemSpawnCommandTarget(world, itemsDiffLog);
        _creatures = new CreatureSpawnCommandTarget(world, creaturesDiffLog);
        _orders = new OrderCommandTarget(world);
        _zones = new ZoneCommandTarget(world);
        _workshops = new WorkshopQueueCommandTarget(world, recipes, constructions);
        _stockpiles = new StockpileCommandTarget(world, log);
    }

    internal IProfessionAssignmentCommandTarget Professions => _professions;
    internal IItemSpawnCommandTarget Items => _items;
    internal ICreatureSpawnCommandTarget Creatures => _creatures;
    internal IOrderCommandTarget Orders => _orders;
    internal IZoneCommandTarget Zones => _zones;
    internal IWorkshopQueueCommandTarget Workshops => _workshops;
    internal IStockpileCommandTarget Stockpiles => _stockpiles;

    void IRuntimeProfessionCommandBindings.SetProfessionWeightHandler(Action<Guid, string, int>? setProfessionWeight)
    {
        _professions.SetHandler(setProfessionWeight);
    }
}
