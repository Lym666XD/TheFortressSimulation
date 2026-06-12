using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.Placeables;
using SadRogue.Primitives;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Construction;

internal sealed class ConstructionMaterialTracker
{
    private readonly WorldModel _world;
    private readonly ConstructionFootprintCells _cells;
    private readonly IItemDefinitionCatalog _itemDefinitions;
    private readonly IConstructionDiffEmitter _diffEmitter;
    private readonly IConstructionJobLogger _logger;

    public ConstructionMaterialTracker(
        WorldModel world,
        ConstructionFootprintCells cells,
        IItemDefinitionCatalog itemDefinitions,
        IConstructionDiffEmitter diffEmitter,
        IConstructionJobLogger? logger)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _cells = cells ?? throw new ArgumentNullException(nameof(cells));
        _itemDefinitions = itemDefinitions ?? throw new ArgumentNullException(nameof(itemDefinitions));
        _diffEmitter = diffEmitter ?? throw new ArgumentNullException(nameof(diffEmitter));
        _logger = logger ?? NullConstructionJobLogger.Instance;
    }

    public Dictionary<string, int> CountDelivered(PlaceableInstance site)
    {
        var delivered = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in _cells.EnumerateFootprintAndRing(site))
        {
            foreach (var item in _world.Items.GetGroundItemsAt(cell, site.Z))
            {
                var def = _itemDefinitions.GetDefinition(item.DefinitionId);
                if (def == null || def.Tags == null)
                {
                    continue;
                }

                foreach (var req in site.ConstructionSite!.MaterialsRequired.Keys)
                {
                    if (ConstructionRequirementMatcher.Matches(def.Tags, req))
                    {
                        delivered[req] = delivered.GetValueOrDefault(req, 0) + item.StackCount;
                        break;
                    }
                }
            }
        }

        return delivered;
    }

    public bool TryConsume(PlaceableInstance site, Dictionary<string, int> toConsume)
    {
        var removals = new List<PlannedItemRemoval>();
        var plannedByItem = new Dictionary<Guid, int>();
        var cells = _cells.EnumerateFootprintAndRing(site).ToList();

        foreach (var cell in cells)
        {
            var itemsAtCell = _world.Items.GetGroundItemsAt(cell, site.Z)
                .OrderBy(item => item.Guid)
                .ToList();

            foreach (var item in itemsAtCell)
            {
                var def = _itemDefinitions.GetDefinition(item.DefinitionId);
                if (def == null || def.Tags == null)
                {
                    continue;
                }

                plannedByItem.TryGetValue(item.Guid, out int alreadyPlanned);
                int available = Math.Max(0, item.StackCount - alreadyPlanned);
                if (available <= 0)
                {
                    continue;
                }

                foreach (var requirement in toConsume.Keys.OrderBy(k => k).ToList())
                {
                    if (toConsume[requirement] <= 0)
                    {
                        continue;
                    }

                    if (!ConstructionRequirementMatcher.Matches(def.Tags, requirement))
                    {
                        continue;
                    }

                    int take = Math.Min(available, toConsume[requirement]);
                    if (take <= 0)
                    {
                        continue;
                    }

                    plannedByItem[item.Guid] = alreadyPlanned + take;
                    removals.Add(new PlannedItemRemoval(item.Guid, item.Position, item.Z, take));
                    toConsume[requirement] -= take;
                    available -= take;
                    alreadyPlanned += take;
                    if (available <= 0)
                    {
                        break;
                    }

                    if (toConsume[requirement] <= 0)
                    {
                        break;
                    }
                }
            }
        }

        if (toConsume.Any(kv => kv.Value > 0))
        {
            _logger.Log($"[BUILD.EXEC] consume failed site=({site.Position.X},{site.Position.Y},{site.Z}) remaining={FormatDict(toConsume)}");
            return false;
        }

        foreach (var removal in removals)
        {
            _diffEmitter.RemoveItem(removal.ItemGuid, removal.Position, removal.Z, removal.Quantity);
        }

        return true;
    }

    public List<(ItemInstance item, int dist, string tags)> GetItemsNearSite(PlaceableInstance site, int radius)
    {
        var result = new List<(ItemInstance, int, string)>();
        foreach (var item in _world.Items.GetGroundInstances())
        {
            int dx = Math.Abs(item.Position.X - site.Position.X);
            int dy = Math.Abs(item.Position.Y - site.Position.Y);
            int dz = Math.Abs(item.Z - site.Z);
            int dist = dx + dy + dz;
            if (dist > radius)
            {
                continue;
            }

            var def = _itemDefinitions.GetDefinition(item.DefinitionId);
            string tags = def?.Tags != null ? string.Join(",", def.Tags) : "none";
            result.Add((item, dist, tags));
        }

        return result;
    }

    private static string FormatDict(Dictionary<string, int> values)
    {
        return "{" + string.Join(", ", values.Select(kv => $"{kv.Key}:{kv.Value}")) + "}";
    }

    private readonly record struct PlannedItemRemoval(Guid ItemGuid, Point Position, int Z, int Quantity);
}
