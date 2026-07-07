using System;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

internal static class MiningPathSeed
{
    internal static uint From(Guid workerId, Point target)
    {
        unchecked
        {
            var bytes = workerId.ToByteArray();
            uint seed = 2166136261;
            foreach (var value in bytes)
            {
                seed = (seed ^ value) * 16777619;
            }

            seed = (seed ^ (uint)target.X) * 16777619;
            seed = (seed ^ (uint)target.Y) * 16777619;
            return seed;
        }
    }
}
