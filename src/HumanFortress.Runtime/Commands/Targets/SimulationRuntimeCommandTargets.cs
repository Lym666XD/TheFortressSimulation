using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Runtime.Content;
using HumanFortress.Runtime.Diff;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Commands;

internal sealed class SimulationRuntimeCommandTargets : IRuntimeProfessionCommandBindings
{
    private readonly RuntimeMutationDiffLogs _mutationDiffs;
    private readonly ProfessionAssignmentCommandTarget _professions;
    private readonly ItemSpawnCommandTarget _items;
    private readonly CreatureSpawnCommandTarget _creatures;
    private readonly OrderCommandTarget _orders;
    private readonly ZoneCommandTarget _zones;
    private readonly WorkshopQueueCommandTarget _workshops;
    private readonly StockpileCommandTarget _stockpiles;

    internal SimulationRuntimeCommandTargets(
        World world,
        RuntimeMutationDiffLogs mutationDiffs,
        IRecipeCatalog recipes,
        FortressRuntimeStockpilePresetCatalog? stockpilePresets = null,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(mutationDiffs);
        ArgumentNullException.ThrowIfNull(recipes);

        _mutationDiffs = mutationDiffs;
        _professions = new ProfessionAssignmentCommandTarget(mutationDiffs.Professions);
        _items = new ItemSpawnCommandTarget(world, mutationDiffs.Items);
        _creatures = new CreatureSpawnCommandTarget(world, mutationDiffs.Creatures);
        _orders = new OrderCommandTarget(mutationDiffs.Orders);
        _zones = new ZoneCommandTarget(mutationDiffs.Zones, log);
        _workshops = new WorkshopQueueCommandTarget(mutationDiffs.Workshops, recipes, log);
        _stockpiles = new StockpileCommandTarget(world, mutationDiffs.Stockpiles, stockpilePresets, log);
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
        _mutationDiffs.Professions.SetProfessionWeightHandler(setProfessionWeight);
    }

}
