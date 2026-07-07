using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class ManagementDrawerSnapshotBuilder
{
    private static ItemDrawerData BuildItems(World world)
    {
        var rows = world.Items.GetGroundInstances()
            .OrderBy(item => item.DefinitionId, StringComparer.Ordinal)
            .ThenBy(item => item.Guid)
            .Select(item => BuildItemRow(world, item))
            .ToList();

        return new ItemDrawerData(
            rows,
            BuildAvailableKinds(world),
            rows.Count,
            rows.Sum(row => row.StackCount));
    }

    private static ItemDrawerRowView BuildItemRow(World world, ItemInstance item)
    {
        var definition = world.Items.GetDefinition(item.DefinitionId);
        string name = definition?.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            name = item.DefinitionId;

        string? materialId = item.MaterialId;
        if (string.IsNullOrWhiteSpace(materialId))
            materialId = definition?.FixedMaterial;

        if (!string.IsNullOrWhiteSpace(materialId) && IsGenericResourceName(name))
            name = $"{MaterialSuffixFriendly(materialId)} {name}";

        return new ItemDrawerRowView(
            item.Guid,
            item.DefinitionId,
            name,
            NormalizeKind(definition?.Kind),
            item.StackCount,
            item.Position.X,
            item.Position.Y,
            item.Z);
    }

    private static IReadOnlyList<string> BuildAvailableKinds(World world)
    {
        var kinds = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddKind("all");
        AddKind("resource");
        AddKind("weapon");
        AddKind("armor");
        AddKind("tool");
        AddKind("container");
        AddKind("consumable");
        AddKind("placeable");
        AddKind("ammo");
        AddKind("siege_weapon");

        foreach (var kind in world.Items.GetAvailableKinds())
            AddKind(kind);

        return kinds;

        void AddKind(string? kind)
        {
            var normalized = NormalizeKind(kind);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (seen.Add(normalized))
                kinds.Add(normalized);
        }
    }
}
