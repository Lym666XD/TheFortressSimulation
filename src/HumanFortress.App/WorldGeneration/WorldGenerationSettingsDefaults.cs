using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.App.WorldGeneration;

internal static class WorldGenerationSettingsDefaults
{
    internal static WorldGenerationSettings CreateDefault()
    {
        return new WorldGenerationSettings
        {
            Name = "World",
            Seed = NewSeed(),
            Width = 256,
            Height = 256,
            Difficulty = WorldGenerationDifficulty.Normal
        };
    }

    internal static uint NewSeed()
    {
        return (uint)Environment.TickCount;
    }
}
