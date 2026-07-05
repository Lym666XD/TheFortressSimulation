using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.WorldGen.Implementation;

internal static class WorldGenerationServiceFactory
{
    internal static IWorldGenerationService Create()
    {
        return new WorldGenerationService();
    }
}
