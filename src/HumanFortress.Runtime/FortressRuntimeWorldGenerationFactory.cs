using HumanFortress.Contracts.WorldGen;
using HumanFortress.WorldGen.Implementation;

namespace HumanFortress.Runtime;

public static class FortressRuntimeWorldGenerationFactory
{
    public static IWorldGenerationService Create()
    {
        return WorldGenerationServiceFactory.Create();
    }
}
