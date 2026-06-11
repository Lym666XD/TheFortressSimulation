using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Jobs.Mining;

internal interface IMiningWorkCostResolver
{
    int CalculateRequiredTicks(ushort geologyHandle, TerrainKind terrainKind);
}
