namespace HumanFortress.Simulation.Tiles;

/// <summary>
/// Canonical terrain bit operations per TILES_MATERIALS_ARCHITECTURE.md.
/// This is the single source of truth for TerrainBits encoding/decoding.
/// </summary>
public static class TerrainBitOps
{
    // Bit layout constants (v2)
    private const int KindMask = 0xF;        // bits 0-3
    private const int KindShift = 0;
    private const int NaturalBit = 5;        // bit 5
    private const int ModifiableBit = 6;     // bit 6
    // other bits reserved (must be 0)

    /// <summary>
    /// Extract TerrainKind from TerrainBits (bits 0-3).
    /// </summary>
    public static TerrainKind GetKind(ushort bits) => (TerrainKind)(bits & KindMask);

    /// <summary>
    /// Check if terrain is natural (bit 6).
    /// </summary>
    public static bool IsNatural(ushort bits) => (bits & (1 << NaturalBit)) != 0;

    /// <summary>
    /// Check if terrain is modifiable (bit 6).
    /// </summary>
    public static bool IsModifiable(ushort bits) => (bits & (1 << ModifiableBit)) != 0;

    /// <summary>
    /// Create TerrainBits from components (v2).
    /// </summary>
    public static ushort CreateTerrainBits(
        TerrainKind kind,
        bool natural = true,
        bool modifiable = true)
    {
        ushort bits = 0;

        // Set terrain kind (bits 0-2)
        bits |= (ushort)(((int)kind & KindMask) << KindShift);

        // Set natural flag (bit 6)
        if (natural)
            bits |= (ushort)(1 << NaturalBit);

        // Set modifiable flag (bit 6)
        if (modifiable)
            bits |= (ushort)(1 << ModifiableBit);

        return bits;
    }

    /// <summary>
    /// Modify terrain kind while preserving other bits.
    /// </summary>
    public static ushort SetKind(ushort bits, TerrainKind kind)
    {
        // Clear kind bits and set new kind
        bits = (ushort)(bits & ~KindMask);
        bits |= (ushort)((int)kind & KindMask);

        return bits;
    }

    /// <summary>
    /// Set natural flag.
    /// </summary>
    public static ushort SetNatural(ushort bits, bool natural)
    {
        if (natural)
            bits |= (ushort)(1 << NaturalBit);
        else
            bits = (ushort)(bits & ~(1 << NaturalBit));

        return bits;
    }

    /// <summary>
    /// Set modifiable flag.
    /// </summary>
    public static ushort SetModifiable(ushort bits, bool modifiable)
    {
        if (modifiable)
            bits |= (ushort)(1 << ModifiableBit);
        else
            bits = (ushort)(bits & ~(1 << ModifiableBit));

        return bits;
    }

    /// <summary>
    /// Get direction offsets for ramp navigation.
    /// Direction encoding: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW.
    /// </summary>
    public static (int dx, int dy) GetDirectionOffset(byte direction)
    {
        return direction switch
        {
            0 => (0, -1),   // North
            1 => (1, -1),   // Northeast
            2 => (1, 0),    // East
            3 => (1, 1),    // Southeast
            4 => (0, 1),    // South
            5 => (-1, 1),   // Southwest
            6 => (-1, 0),   // West
            7 => (-1, -1),  // Northwest
            _ => throw new ArgumentException($"Invalid direction {direction}")
        };
    }

    /// <summary>
    /// Check if approach direction is valid for ascending a ramp (helper used in UI/tests).
    /// </summary>
    public static bool CanAscendRamp(int fromX, int fromY, int rampX, int rampY, byte rampDirection)
    {
        var (dx, dy) = GetDirectionOffset(rampDirection);
        int expectedFromX = rampX - dx;
        int expectedFromY = rampY - dy;

        return fromX == expectedFromX && fromY == expectedFromY;
    }

    /// <summary>
    /// Get destination position when ascending a ramp.
    /// </summary>
    public static (int x, int y, int z) GetRampDestination(int rampX, int rampY, int rampZ, byte rampDirection)
    {
        var (dx, dy) = GetDirectionOffset(rampDirection);
        return (rampX + dx, rampY + dy, rampZ + 1);
    }
}
