using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Jobs.Mining;

internal interface IMiningDropResolver : IMiningWorkCostResolver
{
    ushort ResolveAirGeologyHandle();

    List<(string itemId, int qty)> ChooseDropsFor(ushort geologyHandle, TerrainKind terrainKind);
}
