using HumanFortress.Contracts.WorldGen;
using HumanFortress.WorldGen;

namespace HumanFortress.App.WorldGeneration;

internal sealed class WorldGenerationAccess : IWorldGenerationAccess
{
    private readonly IWorldGenerationService _service = WorldGenerationServiceFactory.Create();

    public event Action<string, float>? ProgressChanged
    {
        add
        {
            if (value != null)
                _service.ProgressChanged += value;
        }
        remove
        {
            if (value != null)
                _service.ProgressChanged -= value;
        }
    }

    public IGeneratedWorldData Generate(WorldGenerationSettings settings)
    {
        return _service.Generate(settings);
    }
}
