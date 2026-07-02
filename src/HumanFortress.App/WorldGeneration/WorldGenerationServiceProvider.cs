using HumanFortress.Contracts.WorldGen;
using HumanFortress.Runtime;

namespace HumanFortress.App.WorldGeneration;

internal static class WorldGenerationServiceProvider
{
    internal static IWorldGenerationService Create()
    {
        return FortressRuntimeWorldGenerationFactory.Create();
    }
}
