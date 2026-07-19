using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.Placeables;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Construction;

internal sealed class ConstructionSiteProgress
{
    private readonly WorldModel _world;
    private readonly ConstructionMaterialTracker _materials;
    private readonly ConstructionTuning _tuning;
    private readonly IConstructionJobLogger _logger;

    internal ConstructionSiteProgress(
        WorldModel world,
        ConstructionMaterialTracker materials,
        ConstructionTuning tuning,
        IConstructionJobLogger? logger)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _logger = logger ?? NullConstructionJobLogger.Instance;
    }

    internal bool AdvanceIfReady(PlaceableInstance site, ulong tick)
    {
        var construction = site.ConstructionSite;
        if (construction == null)
        {
            return false;
        }

        var delivered = _materials.CountDelivered(site, tick);
        construction.MaterialsDelivered = delivered;

        _logger.Log($"[BUILD.EXEC] site=({site.Position.X},{site.Position.Y},{site.Z}) delivered={FormatDict(delivered)} req={FormatDict(construction.GetRequiredMaterialsSnapshot())} progress={construction.BuildProgressTicks}/{construction.TotalBuildTicks}");
        LogNearbyItems(site, tick);

        if (!HasRequiredMaterials(site, construction, delivered))
        {
            return false;
        }

        int rate = Math.Max(1, _tuning.BuildRateTicks);
        construction.BuildProgressTicks = Math.Min(construction.TotalBuildTicks, construction.BuildProgressTicks + rate);
        return construction.BuildProgressTicks >= construction.TotalBuildTicks;
    }

    private void LogNearbyItems(PlaceableInstance site, ulong tick)
    {
        if (tick % 50 != 0)
        {
            return;
        }

        var itemsNear = _materials.GetItemsNearSite(site, 3);
        if (itemsNear.Count == 0)
        {
            return;
        }

        _logger.Log($"[BUILD.DIAG] site=({site.Position.X},{site.Position.Y},{site.Z}) nearby_items:");
        foreach (var (item, dist, tags) in itemsNear)
        {
            bool reserved = _world.Reservations.IsItemReserved(item.Guid, tick);
            _logger.Log($"  - item={item.DefinitionId} stack={item.StackCount} pos=({item.Position.X},{item.Position.Y},{item.Z}) dist={dist} reserved={reserved} tags=[{tags}]");
        }
    }

    private bool HasRequiredMaterials(
        PlaceableInstance site,
        ConstructionSiteState construction,
        Dictionary<string, int> delivered)
    {
        foreach (var requirement in construction.GetRequiredMaterialsSnapshot())
        {
            var tag = requirement.Key;
            var need = requirement.Value;
            int have = delivered.GetValueOrDefault(tag, 0);
            if (have >= need)
            {
                continue;
            }

            _logger.Log($"[BUILD.EXEC] site=({site.Position.X},{site.Position.Y},{site.Z}) NOT READY: need {tag}={need}, have={have}, shortfall={need - have}");
            return false;
        }

        return true;
    }

    private static string FormatDict(IEnumerable<KeyValuePair<string, int>> values)
    {
        return "{" + string.Join(", ", values
            .OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static kv => kv.Key, StringComparer.Ordinal)
            .Select(static kv => $"{kv.Key}:{kv.Value}")) + "}";
    }
}
