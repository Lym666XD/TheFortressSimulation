using System;

namespace HumanFortress.Core.Random;

/// <summary>
/// Deterministic GUID generation for placeable instances.
/// Uses DeterministicRng to ensure replay consistency per DETERMINISM_CI.md.
/// </summary>
public static class DeterministicGuidGenerator
{
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
    /// Hash position to 64-bit value for seed generation.
    /// Uses FNV-1a hash algorithm for good distribution.
    /// </summary>
    private static ulong HashPosition(int x, int y, int z)
    {
        const ulong FNV_OFFSET = 14695981039346656037UL;
        const ulong FNV_PRIME = 1099511628211UL;

        ulong hash = FNV_OFFSET;

        // Hash x coordinate (4 bytes)
        hash ^= (ulong)(x & 0xFF);
        hash *= FNV_PRIME;
        hash ^= (ulong)((x >> 8) & 0xFF);
        hash *= FNV_PRIME;
        hash ^= (ulong)((x >> 16) & 0xFF);
        hash *= FNV_PRIME;
        hash ^= (ulong)((x >> 24) & 0xFF);
        hash *= FNV_PRIME;

        // Hash y coordinate (4 bytes)
        hash ^= (ulong)(y & 0xFF);
        hash *= FNV_PRIME;
        hash ^= (ulong)((y >> 8) & 0xFF);
        hash *= FNV_PRIME;
        hash ^= (ulong)((y >> 16) & 0xFF);
        hash *= FNV_PRIME;
        hash ^= (ulong)((y >> 24) & 0xFF);
        hash *= FNV_PRIME;

        // Hash z coordinate (4 bytes)
        hash ^= (ulong)(z & 0xFF);
        hash *= FNV_PRIME;
        hash ^= (ulong)((z >> 8) & 0xFF);
        hash *= FNV_PRIME;
        hash ^= (ulong)((z >> 16) & 0xFF);
        hash *= FNV_PRIME;
        hash ^= (ulong)((z >> 24) & 0xFF);
        hash *= FNV_PRIME;

        return hash;
    }
}
