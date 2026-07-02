using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.WorldGen;

internal static class WorldGenerationServiceFactory
{
    internal static IWorldGenerationService Create()
    {
        return new WorldGenerationService();
    }
}
