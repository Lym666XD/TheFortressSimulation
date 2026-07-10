using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Core.Random;

/// <summary>
/// Manages named RNG streams for deterministic simulation per DETERMINISM_CI.md.
/// Each system gets its own stream to prevent coupling.
/// </summary>
public sealed class RngStreamManager
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private readonly Dictionary<string, DeterministicRng> _streams = new(StringComparer.Ordinal);
    private readonly ulong _masterSeed;
    private readonly object _sync = new();

    public RngStreamManager(ulong masterSeed)
    {
        _masterSeed = masterSeed;
    }

    /// <summary>
    /// Get or create a named RNG stream.
    /// </summary>
    public DeterministicRng GetStream(string streamName)
    {
        if (string.IsNullOrWhiteSpace(streamName))
            throw new ArgumentException("RNG stream name must be non-empty.", nameof(streamName));

        lock (_sync)
        {
            if (!_streams.TryGetValue(streamName, out var stream))
            {
                stream = new DeterministicRng(DeriveStreamSeed(streamName));
                _streams.Add(streamName, stream);
            }

            return stream;
        }
    }

    private ulong DeriveStreamSeed(string streamName)
    {
        var hash = FnvOffsetBasis;
        foreach (var c in streamName)
        {
            hash ^= (byte)c;
            hash *= FnvPrime;
            hash ^= (byte)(c >> 8);
            hash *= FnvPrime;
        }

        return DeterministicRng.MixSeed(_masterSeed ^ hash);
    }

    /// <summary>
    /// Standard stream names per system.
    /// </summary>
    public static class StreamNames
    {
        public const string WorldGen = "worldgen";
        public const string MapGen = "mapgen";
        public const string Creatures = "creatures";
        public const string Combat = "combat";
        public const string Items = "items";
        public const string Weather = "weather";
        public const string Storyteller = "storyteller";
        public const string AI = "ai";
        public const string Fluids = "fluids";
        public const string Fields = "fields";
    }

    /// <summary>
    /// Save all stream states for deterministic replay.
    /// </summary>
    public Dictionary<string, RngState> SaveStates()
    {
        var states = new Dictionary<string, RngState>();
        foreach (var kvp in GetOrderedStreams())
        {
            states[kvp.Key] = kvp.Value.GetState();
        }
        return states;
    }

    /// <summary>
    /// Get a canonical stream state snapshot sorted by stream name.
    /// </summary>
    public IReadOnlyList<RngStreamStateSnapshot> GetStateSnapshot()
    {
        return GetOrderedStreams()
            .Select(static kvp => new RngStreamStateSnapshot(kvp.Key, kvp.Value.GetState()))
            .ToArray();
    }

    /// <summary>
    /// Clear all materialized streams while keeping the manager's master seed.
    /// </summary>
    public void ClearStreams()
    {
        lock (_sync)
        {
            _streams.Clear();
        }
    }

    /// <summary>
    /// Restore stream states from save.
    /// </summary>
    public void RestoreStates(Dictionary<string, RngState> states)
    {
        foreach (var kvp in states.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
        {
            var stream = GetStream(kvp.Key);
            stream.SetState(kvp.Value);
        }
    }

    /// <summary>
    /// Restore stream states from a canonical stream state snapshot.
    /// </summary>
    public void RestoreStates(IEnumerable<RngStreamStateSnapshot> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in states)
        {
            if (string.IsNullOrWhiteSpace(state.StreamName))
                throw new ArgumentException("RNG stream snapshot contains an empty stream name.", nameof(states));
            if (!seen.Add(state.StreamName))
                throw new ArgumentException($"RNG stream snapshot contains duplicate stream '{state.StreamName}'.", nameof(states));

            var stream = GetStream(state.StreamName);
            stream.SetState(state.State);
        }
    }

    private KeyValuePair<string, DeterministicRng>[] GetOrderedStreams()
    {
        lock (_sync)
        {
            return _streams
                .OrderBy(static kvp => kvp.Key, StringComparer.Ordinal)
                .ToArray();
        }
    }
}

/// <summary>
/// Canonical RNG stream state row for deterministic replay/save snapshots.
/// </summary>
public readonly record struct RngStreamStateSnapshot(string StreamName, RngState State);
