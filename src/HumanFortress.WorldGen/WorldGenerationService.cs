using System;
using HumanFortress.Contracts.WorldGen;
using HumanFortress.Core.World;

namespace HumanFortress.WorldGen.Implementation;

internal sealed class WorldGenerationService : IWorldGenerationService
{
    private readonly WorldGenerator _generator;

    internal WorldGenerationService()
    {
        _generator = new WorldGenerator();
        _generator.ProgressChanged += (stage, progress) => ProgressChanged?.Invoke(stage, progress);
    }

    public event Action<string, float>? ProgressChanged;

    public IGeneratedWorldData Generate(WorldGenerationSettings settings)
    {
        return GeneratedWorldData.FromWorldGenResult(_generator.Generate(ToWorldParams(settings)));
    }

    private static WorldParams ToWorldParams(WorldGenerationSettings settings)
    {
        return new WorldParams
        {
            Name = settings.Name,
            Seed = settings.Seed,
            Width = settings.Width,
            Height = settings.Height,
            Difficulty = ToCoreDifficulty(settings.Difficulty)
        };
    }

    private static DifficultyPreset ToCoreDifficulty(WorldGenerationDifficulty difficulty)
    {
        return difficulty switch
        {
            WorldGenerationDifficulty.Easy => DifficultyPreset.Easy,
            WorldGenerationDifficulty.Normal => DifficultyPreset.Normal,
            WorldGenerationDifficulty.Hard => DifficultyPreset.Hard,
            WorldGenerationDifficulty.Nightmare => DifficultyPreset.Nightmare,
            _ => DifficultyPreset.Normal
        };
    }
}
