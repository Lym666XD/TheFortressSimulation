using System;
using HumanFortress.Contracts.Simulation.Items;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Items;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Projects runtime item state into the narrow data shape stockpile filters need.
/// </summary>
internal static class StockpileItemProjection
{
    internal static ItemStackRef FromItem(ItemInstance item, ItemDefinition? definition, int lastZoneId = 0)
    {
        ArgumentNullException.ThrowIfNull(item);

        var materialId = !string.IsNullOrWhiteSpace(item.MaterialId)
            ? item.MaterialId
            : definition?.FixedMaterial;

        return new ItemStackRef(
            HandleFor(item),
            item.DefinitionId,
            definition?.Tags,
            materialId,
            lastZoneId,
            item.SpawnedAtTick,
            item.ReservationTokens.Count > 0);
    }

    internal static ulong HandleFor(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return DiffTargetEncoding.EntityKey(item.Guid);
    }
}
