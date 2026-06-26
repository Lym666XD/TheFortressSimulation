using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.WorldGen;

public static class WorldGenerationServiceFactory
{
    public static IWorldGenerationService Create()
    {
        return new WorldGenerationService();
    }
}
