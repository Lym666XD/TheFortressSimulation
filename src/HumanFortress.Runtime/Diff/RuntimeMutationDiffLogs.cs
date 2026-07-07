using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Zones;

namespace HumanFortress.Runtime.Diff;

/// <summary>
/// Runtime-owned mutation logs shared by command targets and the post-tick applicators.
/// </summary>
internal sealed class RuntimeMutationDiffLogs
{
    internal RuntimeMutationDiffLogs(
        ItemsDiffLog? items = null,
        CreaturesDiffLog? creatures = null,
        ProfessionAssignmentDiffLog? professions = null,
        OrderDiffLog? orders = null,
        WorkshopDiffLog? workshops = null,
        ZoneDiffLog? zones = null,
        StockpileDiffLog? stockpiles = null)
    {
        Items = items ?? new ItemsDiffLog();
        Creatures = creatures ?? new CreaturesDiffLog();
        Professions = professions ?? new ProfessionAssignmentDiffLog();
        Orders = orders ?? new OrderDiffLog();
        Workshops = workshops ?? new WorkshopDiffLog();
        Zones = zones ?? new ZoneDiffLog();
        Stockpiles = stockpiles ?? new StockpileDiffLog();
    }

    internal ItemsDiffLog Items { get; }
    internal CreaturesDiffLog Creatures { get; }
    internal ProfessionAssignmentDiffLog Professions { get; }
    internal OrderDiffLog Orders { get; }
    internal WorkshopDiffLog Workshops { get; }
    internal ZoneDiffLog Zones { get; }
    internal StockpileDiffLog Stockpiles { get; }

    internal void Clear()
    {
        Items.Clear();
        Creatures.Clear();
        Professions.Clear();
        Orders.Clear();
        Workshops.Clear();
        Zones.Clear();
        Stockpiles.Clear();
    }
}
