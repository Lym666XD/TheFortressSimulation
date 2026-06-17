using HumanFortress.Core.Content.Registry;
using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.App.Jobs;

internal sealed class ConstructionTerrainMaterialResolver : IConstructionTerrainMaterialResolver
{
    private readonly IRuntimeGeologyCatalog _geology;

    public ConstructionTerrainMaterialResolver(IRuntimeGeologyCatalog geology)
    {
        _geology = geology ?? throw new ArgumentNullException(nameof(geology));
    }

    public ushort ResolveGeologyHandle(MaterialFilterSpec filter, TerrainKind kind)
    {
        if (!string.IsNullOrWhiteSpace(filter.PreferredMaterialId)
            && _geology.TryGetGeologyHandleByMaterialAndKind(filter.PreferredMaterialId, kind.ToString(), out var preferredHandle))
        {
            return preferredHandle;
        }

        return 0;
    }

    public ushort TryMatchFromCurrent(TileBase tile, TerrainKind kind)
    {
        try
        {
            var geology = _geology.GetGeologyByHandle(tile.GeoMatId);
            if (geology != null
                && _geology.TryGetGeologyHandleByMaterialAndKind(geology.Material, kind.ToString(), out var handle))
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
