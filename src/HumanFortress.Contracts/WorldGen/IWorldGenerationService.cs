namespace HumanFortress.Contracts.WorldGen;

public interface IWorldGenerationService
{
    event Action<string, float>? ProgressChanged;

    IGeneratedWorldData Generate(WorldGenerationSettings settings);
}
