using HumanFortress.Core.Random;

namespace HumanFortress.Core.Determinism;

/// <summary>
/// Stable field-oriented hash builder for Core-owned RNG stream state.
/// </summary>
public static class RngReplayHashBuilder
{
    public static string Build(RngStreamManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return Build(manager.GetStateSnapshot());
    }

    public static string Build(IEnumerable<RngStreamStateSnapshot> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);

        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("rng.streams.snapshot.v1");
            Append(hash, streams);
        });
    }

    public static void Append(ReplayHashBuilder hash, IEnumerable<RngStreamStateSnapshot> streams)
    {
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(streams);

        var ordered = streams
            .OrderBy(static stream => stream.StreamName, StringComparer.Ordinal)
            .ToArray();

        hash.AddInt32(ordered.Length);
        foreach (var stream in ordered)
        {
            hash.AddString(stream.StreamName);
            hash.AddUInt32(stream.State.S0);
            hash.AddUInt32(stream.State.S1);
            hash.AddUInt32(stream.State.S2);
            hash.AddUInt32(stream.State.S3);
        }
    }
}
