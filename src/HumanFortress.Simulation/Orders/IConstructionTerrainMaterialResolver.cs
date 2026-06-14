using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Simulation.Orders;

public interface IConstructionTerrainMaterialResolver
{
    ushort ResolveGeologyHandle(MaterialFilterSpec filter, TerrainKind kind);

    ushort TryMatchFromCurrent(TileBase tile, TerrainKind kind);
}

public sealed class EmptyConstructionTerrainMaterialResolver : IConstructionTerrainMaterialResolver
{
    public static EmptyConstructionTerrainMaterialResolver Instance { get; } = new();

    private EmptyConstructionTerrainMaterialResolver()
    {
    }

    public ushort ResolveGeologyHandle(MaterialFilterSpec filter, TerrainKind kind)
    {
        return 0;
    }

    public ushort TryMatchFromCurrent(TileBase tile, TerrainKind kind)
    {
        return 0;
    }
}
