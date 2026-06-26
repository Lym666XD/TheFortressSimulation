using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.App.WorldGeneration;

internal interface IWorldGenerationAccess
{
    event Action<string, float>? ProgressChanged;

    IGeneratedWorldData Generate(WorldGenerationSettings settings);
}
