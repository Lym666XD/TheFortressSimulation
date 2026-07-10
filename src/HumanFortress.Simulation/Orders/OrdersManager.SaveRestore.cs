using HumanFortress.Contracts.Simulation.Save;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Orders;

internal sealed partial class OrdersManager
{
    internal IReadOnlyList<string> RestoreActiveSnapshot(
        IReadOnlyList<WorldSaveMiningOrderPayloadData>? mining,
        IReadOnlyList<WorldSaveHaulOrderPayloadData>? hauls,
        IReadOnlyList<WorldSaveConstructionOrderPayloadData>? construction,
        IReadOnlyList<WorldSaveBuildableOrderPayloadData>? buildable)
    {
        var issues = new List<string>();
        if (mining == null) issues.Add("World mining order payload is missing.");
        if (hauls == null) issues.Add("World haul order payload is missing.");
        if (construction == null) issues.Add("World construction order payload is missing.");
        if (buildable == null) issues.Add("World buildable order payload is missing.");
        if (issues.Count > 0)
            return issues;

        ValidateMiningOrders(mining!, issues);
        ValidateHaulOrders(hauls!, issues);
        ValidateConstructionOrders(construction!, issues);
        ValidateBuildableOrders(buildable!, issues);
        if (issues.Count > 0)
            return issues;

        lock (_sync)
        {
            ClearQueue(_haulQueue);
            ClearQueue(_recentHauls);
            ClearList(_activeHauls);
            ClearQueue(_recentMining);
            ClearList(_activeMining);
            ClearQueue(_miningAdd);
            ClearQueue(_miningCancel);
            ClearQueue(_constructionQueue);
            ClearQueue(_recentConstruction);
            ClearList(_activeConstruction);
            ClearQueue(_buildableQueue);
            ClearQueue(_recentBuildable);
            ClearList(_activeBuildable);

            var maxMiningId = 0;
            foreach (var payload in mining!.OrderBy(order => order.Id))
            {
                var designation = new MiningDesignation(
                    payload.Id,
                    ToRectangle(payload.Rect),
                    payload.ZMin,
                    payload.ZMax,
                    (MiningAction)payload.Action,
                    payload.Priority,
                    payload.CreatedTick);
                _miningAdd.Enqueue(designation);
                _recentMining.Enqueue(designation);
                _activeMining.Add(designation);
                maxMiningId = Math.Max(maxMiningId, payload.Id);
            }

            _nextMiningId = maxMiningId;

            foreach (var payload in hauls!
                .OrderBy(order => order.Z)
                .ThenBy(order => order.Priority)
                .ThenBy(order => order.WorldRect.X)
                .ThenBy(order => order.WorldRect.Y)
                .ThenBy(order => order.WorldRect.Width)
                .ThenBy(order => order.WorldRect.Height)
                .ThenBy(order => order.CreatedTick))
            {
                var designation = new HaulDesignation(
                    ToRectangle(payload.WorldRect),
                    payload.Z,
                    payload.Priority,
                    payload.CreatedTick);
                _haulQueue.Enqueue(designation);
                _recentHauls.Enqueue(designation);
                _activeHauls.Add(designation);
            }

            foreach (var payload in construction!
                .OrderBy(order => order.ZMin)
                .ThenBy(order => order.ZMax)
                .ThenBy(order => order.Priority)
                .ThenBy(order => order.WorldRect.X)
                .ThenBy(order => order.WorldRect.Y)
                .ThenBy(order => order.WorldRect.Width)
                .ThenBy(order => order.WorldRect.Height)
                .ThenBy(order => order.Shape)
                .ThenBy(order => order.Filter.CategoryKey, StringComparer.Ordinal)
                .ThenBy(order => order.Filter.PreferredMaterialId, StringComparer.Ordinal)
                .ThenBy(order => BuildMaterialFilterSortKey(order.Filter), StringComparer.Ordinal)
                .ThenBy(order => order.CreatedTick))
            {
                var designation = new ConstructionDesignation(
                    ToRectangle(payload.WorldRect),
                    payload.ZMin,
                    payload.ZMax,
                    (ConstructionShape)payload.Shape,
                    ToMaterialFilter(payload.Filter),
                    payload.Priority,
                    payload.CreatedTick);
                _constructionQueue.Enqueue(designation);
                _recentConstruction.Enqueue(designation);
                _activeConstruction.Add(designation);
            }

            foreach (var payload in buildable!
                .OrderBy(order => order.ConstructionId, StringComparer.Ordinal)
                .ThenBy(order => order.Anchor.X)
                .ThenBy(order => order.Anchor.Y)
                .ThenBy(order => order.Z)
                .ThenBy(order => order.Priority)
                .ThenBy(order => order.CreatedTick))
            {
                var designation = new BuildableConstructionDesignation(
                    payload.ConstructionId,
                    new Point(payload.Anchor.X, payload.Anchor.Y),
                    payload.Z,
                    payload.Priority,
                    payload.CreatedTick);
                _buildableQueue.Enqueue(designation);
                _recentBuildable.Enqueue(designation);
                _activeBuildable.Add(designation);
            }

            TrimRecentQueues();
        }

        return Array.Empty<string>();
    }

    private static void ValidateMiningOrders(
        IReadOnlyList<WorldSaveMiningOrderPayloadData> orders,
        ICollection<string> issues)
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            var prefix = $"World mining order payload[{i}]";
            if (order.Id <= 0)
            {
                issues.Add($"{prefix} has non-positive id {order.Id}.");
            }
            else if (!seen.Add(order.Id))
            {
                issues.Add($"{prefix} duplicates mining id {order.Id}.");
            }

            ValidateRectangle(order.Rect, prefix, issues);

            if (order.ZMin > order.ZMax)
            {
                issues.Add($"{prefix} has zMin greater than zMax.");
            }

            if (!IsDefinedMiningAction(order.Action))
            {
                issues.Add($"{prefix} has unsupported mining action {order.Action}.");
            }
        }
    }

    private static void ValidateHaulOrders(
        IReadOnlyList<WorldSaveHaulOrderPayloadData> orders,
        ICollection<string> issues)
    {
        for (var i = 0; i < orders.Count; i++)
        {
            ValidateRectangle(orders[i].WorldRect, $"World haul order payload[{i}]", issues);
        }
    }

    private static void ValidateConstructionOrders(
        IReadOnlyList<WorldSaveConstructionOrderPayloadData> orders,
        ICollection<string> issues)
    {
        for (var i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            var prefix = $"World construction order payload[{i}]";
            ValidateRectangle(order.WorldRect, prefix, issues);

            if (order.ZMin > order.ZMax)
            {
                issues.Add($"{prefix} has zMin greater than zMax.");
            }

            if (!IsDefinedConstructionShape(order.Shape))
            {
                issues.Add($"{prefix} has unsupported construction shape {order.Shape}.");
            }

            if (string.IsNullOrWhiteSpace(order.Filter.CategoryKey))
            {
                issues.Add($"{prefix} has a blank material filter category key.");
            }
        }
    }

    private static void ValidateBuildableOrders(
        IReadOnlyList<WorldSaveBuildableOrderPayloadData> orders,
        ICollection<string> issues)
    {
        for (var i = 0; i < orders.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(orders[i].ConstructionId))
            {
                issues.Add($"World buildable order payload[{i}] has a blank construction id.");
            }
        }
    }

    private static void ValidateRectangle(
        WorldSaveRectangleData rectangle,
        string prefix,
        ICollection<string> issues)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            issues.Add($"{prefix} has non-positive rectangle dimensions.");
        }
    }

    private static MaterialFilterSpec ToMaterialFilter(WorldSaveMaterialFilterPayloadData payload)
    {
        return new MaterialFilterSpec
        {
            PreferredMaterialId = payload.PreferredMaterialId,
            CategoryKey = payload.CategoryKey,
            Tags = payload.Tags?
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .Order(StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<string>()
        };
    }

    private static string BuildMaterialFilterSortKey(WorldSaveMaterialFilterPayloadData payload)
    {
        return string.Join(
            '\0',
            (payload.Tags ?? Array.Empty<string>())
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .Order(StringComparer.Ordinal));
    }

    private static Rectangle ToRectangle(WorldSaveRectangleData rectangle)
    {
        return new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    private static bool IsDefinedMiningAction(int value)
    {
        return value is >= byte.MinValue and <= byte.MaxValue
            && Enum.IsDefined(typeof(MiningAction), (byte)value);
    }

    private static bool IsDefinedConstructionShape(int value)
    {
        return value is >= byte.MinValue and <= byte.MaxValue
            && Enum.IsDefined(typeof(ConstructionShape), (byte)value);
    }

    private void TrimRecentQueues()
    {
        TrimRecentQueue(_recentHauls);
        TrimRecentQueue(_recentMining);
        TrimRecentQueue(_recentConstruction);
        TrimRecentQueue(_recentBuildable);
    }
}
