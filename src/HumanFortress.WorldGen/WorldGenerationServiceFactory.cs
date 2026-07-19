using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.WorldGen.Implementation;

internal static class WorldGenerationServiceFactory
{
    internal static IWorldGenerationService Create(IDiagnosticSink? diagnostics = null)
    {
        return new WorldGenerationService(diagnostics);
    }
}
