using System.Security.Cryptography;
using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.App.WorldGeneration;

internal static class WorldGenerationSettingsDefaults
{
    private static readonly int[] RandomSizes = { 128, 256, 512 };

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
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt32(bytes);
    }

    internal static int RandomSize()
    {
        return RandomSizes[RandomNumberGenerator.GetInt32(RandomSizes.Length)];
    }

    internal static WorldGenerationDifficulty RandomDifficulty()
    {
        return (WorldGenerationDifficulty)RandomNumberGenerator.GetInt32(0, 4);
    }
}
