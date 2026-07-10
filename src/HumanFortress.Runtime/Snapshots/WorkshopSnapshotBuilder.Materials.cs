using HumanFortress.Simulation.Placeables;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class WorkshopSnapshotBuilder
{
    private static string FormatMaterialProgress(World world, PlaceableInstance site)
    {
        var delivered = CountDeliveredOnFootprintOrRing(world, site);
        var construction = site.ConstructionSite;
        if (construction == null)
            return string.Empty;

        var required = construction.GetRequiredMaterialsSnapshot();
        int deliveredBlocks = delivered.TryGetValue("block", out var blockDelivered) ? blockDelivered : 0;
        int deliveredPlanks = delivered.TryGetValue("plank", out var plankDelivered) ? plankDelivered : 0;
        int requiredBlocks = GetRequiredCount(required, "block");
        int requiredPlanks = GetRequiredCount(required, "plank");
        return $"B {deliveredBlocks}/{requiredBlocks} | P {deliveredPlanks}/{requiredPlanks}";
    }

    private static int GetRequiredCount(IEnumerable<KeyValuePair<string, int>> required, string materialId)
    {
        foreach (var requirement in required)
        {
            if (string.Equals(requirement.Key, materialId, StringComparison.OrdinalIgnoreCase))
                return requirement.Value;
        }

        return 0;
    }

    private static Dictionary<string, int> CountDeliveredOnFootprintOrRing(World world, PlaceableInstance site)
    {
        var delivered = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var footprint = site.Footprint;
        var seen = new HashSet<(int X, int Y)>();

        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int wx = site.Position.X + dx;
                int wy = site.Position.Y + dy;
                if (seen.Add((wx, wy)))
                    AddDeliveredAt(world, site.Z, wx, wy, site, delivered);
            }
        }

        var directions = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int wx = site.Position.X + dx;
                int wy = site.Position.Y + dy;
                foreach (var (adx, ady) in directions)
                {
                    int nx = wx + adx;
                    int ny = wy + ady;
                    if (!world.IsValidPosition(nx, ny, site.Z))
                        continue;

                    if (seen.Add((nx, ny)))
                        AddDeliveredAt(world, site.Z, nx, ny, site, delivered);
                }
            }
        }

        return delivered;
    }

    private static void AddDeliveredAt(World world, int z, int x, int y, PlaceableInstance site, Dictionary<string, int> delivered)
    {
        foreach (var item in world.Items.GetGroundItemsAt(new Point(x, y), z))
        {
            var definition = world.Items.GetDefinition(item.DefinitionId);
            if (definition == null || definition.Tags == null)
                continue;

            foreach (var requirement in site.ConstructionSite!.GetRequiredMaterialIdsSnapshot())
            {
                if (WorkshopSnapshotRules.MaterialMatchesRequirement(definition, requirement))
                {
                    delivered[requirement] = delivered.GetValueOrDefault(requirement) + item.StackCount;
                    break;
                }
            }
        }
    }
}
