using HumanFortress.Contracts.Simulation.Save;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadRestorer
{
    private static void ValidateOrderPayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        ValidateMiningOrderPayloads(payload, issues);
        ValidateHaulOrderPayloads(payload, issues);
        ValidateConstructionOrderPayloads(payload, issues);
        ValidateBuildableOrderPayloads(payload, issues);
    }

    private static void ValidateMiningOrderPayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.MiningOrders == null
            || payload.Counts.MiningOrderCount != payload.MiningOrders.Length)
        {
            return;
        }

        var seen = new HashSet<int>();
        for (var i = 0; i < payload.MiningOrders.Length; i++)
        {
            var order = payload.MiningOrders[i];
            var prefix = $"World mining order payload[{i}]";

            if (order.Id <= 0)
            {
                issues.Add($"{prefix} has non-positive id {order.Id}.");
            }
            else if (!seen.Add(order.Id))
            {
                issues.Add($"{prefix} duplicates mining id {order.Id}.");
            }

            ValidateWorldRectangle(order.Rect, prefix, payload, issues);
            ValidateWorldZRange(order.ZMin, order.ZMax, prefix, payload, issues);
        }
    }

    private static void ValidateHaulOrderPayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.HaulOrders == null
            || payload.Counts.HaulOrderCount != payload.HaulOrders.Length)
        {
            return;
        }

        for (var i = 0; i < payload.HaulOrders.Length; i++)
        {
            var order = payload.HaulOrders[i];
            var prefix = $"World haul order payload[{i}]";
            ValidateWorldRectangle(order.WorldRect, prefix, payload, issues);
            ValidateWorldZ(order.Z, prefix, payload, issues);
        }
    }

    private static void ValidateConstructionOrderPayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.ConstructionOrders == null
            || payload.Counts.ConstructionOrderCount != payload.ConstructionOrders.Length)
        {
            return;
        }

        for (var i = 0; i < payload.ConstructionOrders.Length; i++)
        {
            var order = payload.ConstructionOrders[i];
            var prefix = $"World construction order payload[{i}]";
            ValidateWorldRectangle(order.WorldRect, prefix, payload, issues);
            ValidateWorldZRange(order.ZMin, order.ZMax, prefix, payload, issues);
            ValidateMaterialFilter(order.Filter, prefix, issues);
        }
    }

    private static void ValidateBuildableOrderPayloads(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.BuildableOrders == null
            || payload.Counts.BuildableOrderCount != payload.BuildableOrders.Length)
        {
            return;
        }

        for (var i = 0; i < payload.BuildableOrders.Length; i++)
        {
            var order = payload.BuildableOrders[i];
            var prefix = $"World buildable order payload[{i}]";

            if (string.IsNullOrWhiteSpace(order.ConstructionId))
            {
                issues.Add($"{prefix} has a blank construction id.");
            }

            ValidateWorldPoint(order.Anchor, $"{prefix} anchor", payload, issues);
            ValidateWorldZ(order.Z, prefix, payload, issues);
        }
    }

    private static void ValidateMaterialFilter(
        WorldSaveMaterialFilterPayloadData filter,
        string prefix,
        ICollection<string> issues)
    {
        if (string.IsNullOrWhiteSpace(filter.CategoryKey))
        {
            issues.Add($"{prefix} has a blank material filter category key.");
        }

        ValidateStringArray(filter.Tags, $"{prefix} material filter tags", issues);
    }
}
