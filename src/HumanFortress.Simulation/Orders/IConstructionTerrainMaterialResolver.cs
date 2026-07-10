using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Simulation.Orders;

internal interface IConstructionTerrainMaterialResolver
{
    ushort ResolveGeologyHandle(MaterialFilterSpec filter, TerrainKind kind);

    ushort TryMatchFromCurrent(TileBase tile, TerrainKind kind);
}

internal sealed class EmptyConstructionTerrainMaterialResolver : IConstructionTerrainMaterialResolver
{
    internal static EmptyConstructionTerrainMaterialResolver Instance { get; } = new();

    private EmptyConstructionTerrainMaterialResolver()
    {
    }

    internal ushort ResolveGeologyHandle(MaterialFilterSpec filter, TerrainKind kind)
    {
        return 0;
    }

    ushort IConstructionTerrainMaterialResolver.ResolveGeologyHandle(MaterialFilterSpec filter, TerrainKind kind)
    {
        return ResolveGeologyHandle(filter, kind);
    }

    internal ushort TryMatchFromCurrent(TileBase tile, TerrainKind kind)
    {
        return 0;
    }

    ushort IConstructionTerrainMaterialResolver.TryMatchFromCurrent(TileBase tile, TerrainKind kind)
    {
        return TryMatchFromCurrent(tile, kind);
    }
}
