using HumanFortress.Core.Determinism;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Replay;

internal static class OrdersReplayHashBuilder
{
    internal static string Build(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var mining = world.Orders.GetActiveMiningSnapshot();
        var hauls = world.Orders.GetActiveHaulsSnapshot();
        var construction = world.Orders.GetActiveConstructionSnapshot();
        var buildable = world.Orders.GetActiveBuildableSnapshot();

        return Build(mining, hauls, construction, buildable);
    }

    internal static string Build(
        IReadOnlyList<OrdersManager.MiningDesignation> mining,
        IReadOnlyList<HaulDesignation> hauls,
        IReadOnlyList<ConstructionDesignation> construction,
        IReadOnlyList<BuildableConstructionDesignation> buildable)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("orders.snapshot.v2");
            Append(hash, mining, hauls, construction, buildable);
        });
    }

    internal static void Append(ReplayHashBuilder hash, SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(world);

        hash.AddString("orders.snapshot.v2");
        Append(
            hash,
            world.Orders.GetActiveMiningSnapshot(),
            world.Orders.GetActiveHaulsSnapshot(),
            world.Orders.GetActiveConstructionSnapshot(),
            world.Orders.GetActiveBuildableSnapshot());
    }

    internal static void Append(
        ReplayHashBuilder hash,
        IReadOnlyList<OrdersManager.MiningDesignation> mining,
        IReadOnlyList<HaulDesignation> hauls,
        IReadOnlyList<ConstructionDesignation> construction,
        IReadOnlyList<BuildableConstructionDesignation> buildable)
    {
        ArgumentNullException.ThrowIfNull(hash);

        AddMiningHash(hash, mining);
        AddHaulHash(hash, hauls);
        AddConstructionHash(hash, construction);
        AddBuildableHash(hash, buildable);
    }

    private static void AddMiningHash(ReplayHashBuilder hash, IReadOnlyList<OrdersManager.MiningDesignation> mining)
    {
        var ordered = mining
            .OrderBy(d => d.Id)
            .ThenBy(d => d.ZMin)
            .ThenBy(d => d.ZMax)
            .ThenBy(d => d.Priority)
            .ToArray();
        hash.AddInt32(ordered.Length);
        foreach (var designation in ordered)
        {
            hash.AddInt32(designation.Id);
            AddRectangleHash(hash, designation.Rect);
            hash.AddInt32(designation.ZMin);
            hash.AddInt32(designation.ZMax);
            hash.AddByte((byte)designation.Action);
            hash.AddInt32(designation.Priority);
            hash.AddUInt64(designation.CreatedTick);
        }
    }

    private static void AddHaulHash(ReplayHashBuilder hash, IReadOnlyList<HaulDesignation> hauls)
    {
        var ordered = hauls
            .OrderBy(d => d.Z)
            .ThenBy(d => d.Priority)
            .ThenBy(d => d.WorldRect.X)
            .ThenBy(d => d.WorldRect.Y)
            .ThenBy(d => d.WorldRect.Width)
            .ThenBy(d => d.WorldRect.Height)
            .ThenBy(d => d.CreatedTick)
            .ToArray();
        hash.AddInt32(ordered.Length);
        foreach (var designation in ordered)
        {
            AddRectangleHash(hash, designation.WorldRect);
            hash.AddInt32(designation.Z);
            hash.AddInt32(designation.Priority);
            hash.AddUInt64(designation.CreatedTick);
        }
    }

    private static void AddConstructionHash(ReplayHashBuilder hash, IReadOnlyList<ConstructionDesignation> construction)
    {
        var ordered = construction
            .OrderBy(d => d.ZMin)
            .ThenBy(d => d.ZMax)
            .ThenBy(d => d.Priority)
            .ThenBy(d => d.WorldRect.X)
            .ThenBy(d => d.WorldRect.Y)
            .ThenBy(d => d.WorldRect.Width)
            .ThenBy(d => d.WorldRect.Height)
            .ThenBy(d => d.Shape)
            .ThenBy(d => d.Filter.CategoryKey, StringComparer.Ordinal)
            .ThenBy(d => d.Filter.PreferredMaterialId, StringComparer.Ordinal)
            .ThenBy(d => BuildMaterialFilterSortKey(d.Filter), StringComparer.Ordinal)
            .ThenBy(d => d.CreatedTick)
            .ToArray();
        hash.AddInt32(ordered.Length);
        foreach (var designation in ordered)
        {
            AddRectangleHash(hash, designation.WorldRect);
            hash.AddInt32(designation.ZMin);
            hash.AddInt32(designation.ZMax);
            hash.AddByte((byte)designation.Shape);
            hash.AddNullableString(designation.Filter.PreferredMaterialId);
            hash.AddString(designation.Filter.CategoryKey);
            AddStringArrayHash(hash, designation.Filter.Tags);
            AddMaterialRequirementsHash(hash, designation.Filter.Requirements);
            hash.AddInt32(designation.Priority);
            hash.AddUInt64(designation.CreatedTick);
        }
    }

    private static void AddBuildableHash(ReplayHashBuilder hash, IReadOnlyList<BuildableConstructionDesignation> buildable)
    {
        var ordered = buildable
            .OrderBy(d => d.ConstructionId, StringComparer.Ordinal)
            .ThenBy(d => d.Anchor.X)
            .ThenBy(d => d.Anchor.Y)
            .ThenBy(d => d.Z)
            .ThenBy(d => d.Priority)
            .ThenBy(d => d.CreatedTick)
            .ToArray();
        hash.AddInt32(ordered.Length);
        foreach (var designation in ordered)
        {
            hash.AddString(designation.ConstructionId);
            hash.AddInt32(designation.Anchor.X);
            hash.AddInt32(designation.Anchor.Y);
            hash.AddInt32(designation.Z);
            hash.AddInt32(designation.Priority);
            hash.AddUInt64(designation.CreatedTick);
        }
    }

    private static void AddRectangleHash(ReplayHashBuilder hash, Rectangle rect)
    {
        hash.AddInt32(rect.X);
        hash.AddInt32(rect.Y);
        hash.AddInt32(rect.Width);
        hash.AddInt32(rect.Height);
    }

    private static void AddStringArrayHash(ReplayHashBuilder hash, IEnumerable<string> values)
    {
        var ordered = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Order(StringComparer.Ordinal)
            .ToArray();
        hash.AddInt32(ordered.Length);
        foreach (var value in ordered)
        {
            hash.AddString(value);
        }
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

    private static void AddMaterialRequirementsHash(
        ReplayHashBuilder hash,
        IEnumerable<MaterialRequirementSpec> requirements)
    {
        var ordered = requirements
            .Where(static requirement =>
                !string.IsNullOrWhiteSpace(requirement.Tag)
                || !string.IsNullOrWhiteSpace(requirement.DefinitionId))
            .OrderBy(static requirement => requirement.Tag ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static requirement => requirement.DefinitionId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static requirement => requirement.Count)
            .ToArray();
        hash.AddInt32(ordered.Length);
        foreach (var requirement in ordered)
        {
            hash.AddNullableString(requirement.Tag);
            hash.AddNullableString(requirement.DefinitionId);
            hash.AddInt32(requirement.Count);
        }
    }
}
