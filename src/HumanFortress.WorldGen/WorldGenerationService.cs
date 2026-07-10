using System;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Contracts.WorldGen;
using HumanFortress.Core.World;

namespace HumanFortress.WorldGen.Implementation;

internal sealed class WorldGenerationService : IWorldGenerationService
{
    private readonly WorldGenerator _generator;

    internal WorldGenerationService(IDiagnosticSink? diagnostics = null)
    {
        _generator = new WorldGenerator(diagnostics);
        _generator.ProgressChanged += (stage, progress) => ProgressChanged?.Invoke(stage, progress);
    }

    internal event Action<string, float>? ProgressChanged;

    event Action<string, float>? IWorldGenerationService.ProgressChanged
    {
        add => ProgressChanged += value;
        remove => ProgressChanged -= value;
    }

    internal IGeneratedWorldData Generate(WorldGenerationSettings settings)
    {
        return GeneratedWorldData.FromWorldGenResult(_generator.Generate(ToWorldParams(settings)));
    }

    IGeneratedWorldData IWorldGenerationService.Generate(WorldGenerationSettings settings)
    {
        return Generate(settings);
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
