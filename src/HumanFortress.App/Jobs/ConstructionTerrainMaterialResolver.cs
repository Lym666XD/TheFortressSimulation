using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.App.Jobs;

internal sealed class ConstructionTerrainMaterialResolver : IConstructionTerrainMaterialResolver
{
    public ushort ResolveGeologyHandle(MaterialFilterSpec filter, TerrainKind kind)
    {
        var content = ContentRegistry.Instance;

        var last = MaterialSelectionService.GetLastUsed(filter.CategoryKey);
        if (!string.IsNullOrWhiteSpace(last)
            && content.TryGetGeologyHandleByMaterialAndKind(last, kind.ToString(), out var lastHandle))
        {
            return lastHandle;
        }

        if (!string.IsNullOrWhiteSpace(filter.PreferredMaterialId)
            && content.TryGetGeologyHandleByMaterialAndKind(filter.PreferredMaterialId, kind.ToString(), out var preferredHandle))
        {
            return preferredHandle;
        }

        return 0;
    }

    public ushort TryMatchFromCurrent(TileBase tile, TerrainKind kind)
    {
        try
        {
            var content = ContentRegistry.Instance;
            var geology = content.GetGeologyByHandle(tile.GeoMatId);
            if (geology != null
                && content.TryGetGeologyHandleByMaterialAndKind(geology.Material, kind.ToString(), out var handle))
            {
                return handle;
            }
        }
        catch
        {
        }

        return 0;
    }
}
