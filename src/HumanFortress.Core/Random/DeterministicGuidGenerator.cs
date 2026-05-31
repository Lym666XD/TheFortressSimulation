using System;

namespace HumanFortress.Core.Random;

/// <summary>
/// Deterministic GUID generation for runtime instances.
/// Uses DeterministicRng to ensure replay consistency per DETERMINISM_CI.md.
/// </summary>
public static class DeterministicGuidGenerator
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>
    /// Generate deterministic GUID from RNG state.
    /// GUID structure: 128-bit (16 bytes) = 4x uint32
    /// </summary>
    public static Guid Generate(DeterministicRng rng)
    {
        // Generate 4x uint32 to fill 128-bit GUID
        uint a = rng.Next();
        uint b = rng.Next();
        uint c = rng.Next();
        uint d = rng.Next();

        // Pack into byte array (little-endian)
        byte[] bytes = new byte[16];
        BitConverter.GetBytes(a).CopyTo(bytes, 0);
        BitConverter.GetBytes(b).CopyTo(bytes, 4);
        BitConverter.GetBytes(c).CopyTo(bytes, 8);
        BitConverter.GetBytes(d).CopyTo(bytes, 12);

        return new Guid(bytes);
    }

    /// <summary>
    /// Generate deterministic GUID from position-based seed.
    /// Seed = tickSeed ^ HashPosition(x, y, z)
    /// </summary>
    public static Guid GenerateFromPosition(ulong tickSeed, int x, int y, int z)
    {
        ulong positionHash = HashPosition(x, y, z);
        ulong seed = tickSeed ^ positionHash;
        var rng = new DeterministicRng(seed);
        return Generate(rng);
    }

    /// <summary>
    /// Generate deterministic GUID from a scoped monotonic sequence.
    /// </summary>
    public static Guid GenerateFromSequence(ulong scopeSeed, ulong sequence)
    {
        ulong seed = scopeSeed ^ HashUInt64(sequence);
        var rng = new DeterministicRng(seed);
        return Generate(rng);
    }

    /// <summary>
    /// Generate deterministic GUID from a source GUID and salt.
    /// </summary>
    public static Guid GenerateFromGuid(ulong scopeSeed, Guid sourceGuid, ulong salt)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!sourceGuid.TryWriteBytes(bytes))
        {
            throw new ArgumentException("Unable to write source GUID bytes.", nameof(sourceGuid));
        }

        ulong hash = FnvOffset;
        for (int i = 0; i < bytes.Length; i++)
        {
            hash = HashByte(hash, bytes[i]);
        }

        hash ^= HashUInt64(salt);
        ulong seed = scopeSeed ^ hash;
        var rng = new DeterministicRng(seed);
        return Generate(rng);
    }

    /// <summary>
    /// Hash position to 64-bit value for seed generation.
    /// Uses FNV-1a hash algorithm for good distribution.
    /// </summary>
    private static ulong HashPosition(int x, int y, int z)
    {
        ulong hash = FnvOffset;
        hash = HashInt32(hash, x);
        hash = HashInt32(hash, y);
        hash = HashInt32(hash, z);
        return hash;
    }

    private static ulong HashUInt64(ulong value)
    {
        ulong hash = FnvOffset;
        for (int shift = 0; shift < 64; shift += 8)
        {
            hash = HashByte(hash, (byte)(value >> shift));
        }

        return hash;
    }

    private static ulong HashInt32(ulong hash, int value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            hash = HashByte(hash, (byte)(value >> shift));
        }

        return hash;
    }

    private static ulong HashByte(ulong hash, byte value)
    {
        unchecked
        {
            hash ^= value;
            hash *= FnvPrime;
            return hash;
        }
    }
}
