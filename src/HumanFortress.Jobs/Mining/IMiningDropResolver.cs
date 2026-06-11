using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Jobs.Mining;

internal interface IMiningDropResolver : IMiningWorkCostResolver
{
    List<(string itemId, int qty)> ChooseDropsFor(ushort geologyHandle, TerrainKind terrainKind);
}
