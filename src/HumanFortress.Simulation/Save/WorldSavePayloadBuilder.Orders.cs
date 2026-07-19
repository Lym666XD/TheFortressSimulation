using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.Orders;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadBuilder
{
    private static WorldSaveMiningOrderPayloadData ToPayloadMiningOrder(OrdersManager.MiningDesignation order)
    {
        return new WorldSaveMiningOrderPayloadData(
            order.Id,
            ToPayloadRectangle(order.Rect),
            order.ZMin,
            order.ZMax,
            (int)order.Action,
            order.Priority,
            order.CreatedTick);
    }

    private static WorldSaveHaulOrderPayloadData ToPayloadHaulOrder(HaulDesignation order)
    {
        return new WorldSaveHaulOrderPayloadData(
            ToPayloadRectangle(order.WorldRect),
            order.Z,
            order.Priority,
            order.CreatedTick);
    }

    private static WorldSaveConstructionOrderPayloadData ToPayloadConstructionOrder(
        ConstructionDesignation order)
    {
        return new WorldSaveConstructionOrderPayloadData(
            ToPayloadRectangle(order.WorldRect),
            order.ZMin,
            order.ZMax,
            (int)order.Shape,
            ToPayloadMaterialFilter(order.Filter),
            order.Priority,
            order.CreatedTick);
    }

    private static WorldSaveBuildableOrderPayloadData ToPayloadBuildableOrder(
        BuildableConstructionDesignation order)
    {
        return new WorldSaveBuildableOrderPayloadData(
            order.ConstructionId,
            ToPayloadPoint(order.Anchor),
            order.Z,
            order.Priority,
            order.CreatedTick);
    }

    private static WorldSaveMaterialFilterPayloadData ToPayloadMaterialFilter(MaterialFilterSpec filter)
    {
        return new WorldSaveMaterialFilterPayloadData(
            filter.PreferredMaterialId,
            filter.CategoryKey,
            ToSortedArray(filter.Tags),
            filter.Requirements
                .Where(static requirement =>
                    !string.IsNullOrWhiteSpace(requirement.Tag)
                    || !string.IsNullOrWhiteSpace(requirement.DefinitionId))
                .OrderBy(static requirement => requirement.Tag ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static requirement => requirement.DefinitionId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static requirement => requirement.Count)
                .Select(static requirement => new WorldSaveMaterialRequirementPayloadData(
                    requirement.Tag,
                    requirement.DefinitionId,
                    requirement.Count))
                .ToArray());
    }

    private static string BuildMaterialFilterSortKey(MaterialFilterSpec filter)
    {
        return string.Join(
            '\0',
            filter.Tags.Order(StringComparer.Ordinal)
                .Concat(filter.Requirements
                    .OrderBy(static requirement => requirement.Tag ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(static requirement => requirement.DefinitionId ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(static requirement => requirement.Count)
                    .Select(static requirement => $"{requirement.Tag}|{requirement.DefinitionId}|{requirement.Count}")));
    }
}
