using HumanFortress.Core.Determinism;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Replay;

internal static partial class WorldReplayHashBuilder
{
    private static void AddPointHash(ReplayHashBuilder hash, Point point)
    {
        hash.AddInt32(point.X);
        hash.AddInt32(point.Y);
    }

    private static void AddChunkKeyHash(ReplayHashBuilder hash, ChunkKey key)
    {
        hash.AddInt32(key.ChunkX);
        hash.AddInt32(key.ChunkY);
        hash.AddInt32(key.Z);
    }

    private static void AddStringSetHash(ReplayHashBuilder hash, IEnumerable<string> values)
    {
        var ordered = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var value in ordered)
        {
            hash.AddString(value);
        }
    }

    private static void AddNullableGuid(ReplayHashBuilder hash, Guid? value)
    {
        hash.AddBoolean(value.HasValue);
        if (value.HasValue)
        {
            hash.AddGuid(value.Value);
        }
    }

    private static void AddNullableInt32(ReplayHashBuilder hash, int? value)
    {
        hash.AddBoolean(value.HasValue);
        if (value.HasValue)
        {
            hash.AddInt32(value.Value);
        }
    }
}
