namespace HumanFortress.Core.Random;

/// <summary>
/// Deterministic random number generator using xoshiro128++ algorithm.
/// Fast, high-quality PRNG suitable for games per DETERMINISM_CI.md.
/// </summary>
public sealed class DeterministicRng
{
    private uint _s0, _s1, _s2, _s3;

    /// <summary>
    /// Initialize with a seed value.
    /// </summary>
    public DeterministicRng(ulong seed)
    {
        // Split 64-bit seed into 4x32-bit values
        _s0 = (uint)(seed & 0xFFFFFFFF);
        _s1 = (uint)(seed >> 32);
        _s2 = (uint)(seed ^ 0x9E3779B97F4A7C15UL); // Golden ratio
        _s3 = (uint)((seed ^ 0x9E3779B97F4A7C15UL) >> 32);

        // Warm up the generator
        for (int i = 0; i < 8; i++)
            Next();
    }

    /// <summary>
    /// Get the next random 32-bit value.
    /// </summary>
    public uint Next()
    {
        uint result = RotateLeft(_s0 + _s3, 7) + _s0;
        uint t = _s1 << 9;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;

        _s2 ^= t;
        _s3 = RotateLeft(_s3, 11);

        return result;
    }

    /// <summary>
    /// Get a random integer in range [0, max).
    /// </summary>
    public int NextInt(int max)
    {
        if (max <= 0)
            throw new ArgumentException("Max must be positive", nameof(max));

        // Use rejection sampling for uniform distribution
        uint range = (uint)max;
        uint limit = uint.MaxValue - (uint.MaxValue % range);
        uint value;

        do
        {
            value = Next();
        } while (value >= limit);

        return (int)(value % range);
    }

    /// <summary>
    /// Get a random integer in range [min, max).
    /// </summary>
    public int NextInt(int min, int max)
    {
        if (max <= min)
            throw new ArgumentException("Max must be greater than min");

        return min + NextInt(max - min);
    }

    /// <summary>
    /// Get a random float in range [0, 1).
    /// </summary>
    public float NextFloat()
    {
        return Next() * (1.0f / 4294967296.0f);
    }

    /// <summary>
    /// Get a random double in range [0, 1).
    /// </summary>
    public double NextDouble()
    {
        ulong x = ((ulong)Next() << 32) | Next();
        return x * (1.0 / 18446744073709551616.0);
    }

    /// <summary>
    /// Get a random boolean.
    /// </summary>
    public bool NextBool()
    {
        return (Next() & 1) == 1;
    }

    /// <summary>
    /// Save RNG state for deterministic replay.
    /// </summary>
    public RngState GetState()
    {
        return new RngState(_s0, _s1, _s2, _s3);
    }

    /// <summary>
    /// Restore RNG state from save.
    /// </summary>
    public void SetState(RngState state)
    {
        _s0 = state.S0;
        _s1 = state.S1;
        _s2 = state.S2;
        _s3 = state.S3;
    }

    private static uint RotateLeft(uint x, int k)
    {
        return (x << k) | (x >> (32 - k));
    }
}

/// <summary>
/// Serializable RNG state.
/// </summary>
public readonly struct RngState
{
    public readonly uint S0, S1, S2, S3;

    public RngState(uint s0, uint s1, uint s2, uint s3)
    {
        S0 = s0;
        S1 = s1;
        S2 = s2;
        S3 = s3;
    }
}