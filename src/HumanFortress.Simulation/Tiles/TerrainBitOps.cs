namespace HumanFortress.Simulation.Tiles;

/// <summary>
/// Canonical terrain bit operations per TILES_MATERIALS_ARCHITECTURE.md.
/// This is the single source of truth for TerrainBits encoding/decoding.
/// </summary>
public static class TerrainBitOps
{
    // Bit layout constants
    private const int KindMask = 0x7;        // bits 0-2
    private const int KindShift = 0;
    private const int RampDirMask = 0x7;     // bits 3-5
    private const int RampDirShift = 3;
    private const int NaturalBit = 6;        // bit 6
    private const int SmoothedBit = 7;       // bit 7
    private const int EngravedBit = 8;       // bit 8
    // bits 9-15 reserved (must be 0)

    /// <summary>
    /// Extract TerrainKind from TerrainBits (bits 0-2).
    /// </summary>
    public static TerrainKind GetKind(ushort bits) => (TerrainKind)(bits & KindMask);

    /// <summary>
    /// Extract RampDirection from TerrainBits (bits 3-5).
    /// Only valid when GetKind returns TerrainKind.Ramp.
    /// </summary>
    public static byte GetRampDirection(ushort bits) => (byte)((bits >> RampDirShift) & RampDirMask);

    /// <summary>
    /// Check if terrain is natural (bit 6).
    /// </summary>
    public static bool IsNatural(ushort bits) => (bits & (1 << NaturalBit)) != 0;

    /// <summary>
    /// Check if terrain is smoothed (bit 7).
    /// </summary>
    public static bool IsSmoothed(ushort bits) => (bits & (1 << SmoothedBit)) != 0;

    /// <summary>
    /// Check if terrain is engraved (bit 8).
    /// </summary>
    public static bool IsEngraved(ushort bits) => (bits & (1 << EngravedBit)) != 0;

    /// <summary>
    /// Check if ramp direction is valid (only when kind == Ramp).
    /// </summary>
    public static bool HasValidRampDirection(ushort bits)
    {
        var kind = GetKind(bits);
        if (kind != TerrainKind.Ramp)
            return false;

        var dir = GetRampDirection(bits);
        return dir <= 7; // All values 0-7 are valid compass directions
    }

    /// <summary>
    /// Create TerrainBits from components.
    /// </summary>
    public static ushort CreateTerrainBits(
        TerrainKind kind,
        byte rampDirection = 0,
        bool natural = true,
        bool smoothed = false,
        bool engraved = false)
    {
        // Validate ramp direction
        if (kind == TerrainKind.Ramp && rampDirection > 7)
            throw new ArgumentException($"Invalid ramp direction {rampDirection}, must be 0-7");

        if (kind != TerrainKind.Ramp && rampDirection != 0)
            throw new ArgumentException($"Non-ramp terrain kind {kind} cannot have ramp direction");

        ushort bits = 0;

        // Set terrain kind (bits 0-2)
        bits |= (ushort)((int)kind & KindMask);

        // Set ramp direction (bits 3-5) - only for ramps
        if (kind == TerrainKind.Ramp)
            bits |= (ushort)((rampDirection & RampDirMask) << RampDirShift);

        // Set natural flag (bit 6)
        if (natural)
            bits |= (ushort)(1 << NaturalBit);

        // Set smoothed flag (bit 7)
        if (smoothed)
            bits |= (ushort)(1 << SmoothedBit);

        // Set engraved flag (bit 8)
        if (engraved)
            bits |= (ushort)(1 << EngravedBit);

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

        // Clear ramp direction if not a ramp
        if (kind != TerrainKind.Ramp)
        {
            bits = (ushort)(bits & ~(RampDirMask << RampDirShift));
        }

        return bits;
    }

    /// <summary>
    /// Modify ramp direction (only valid for Ramp terrain).
    /// </summary>
    public static ushort SetRampDirection(ushort bits, byte direction)
    {
        var kind = GetKind(bits);
        if (kind != TerrainKind.Ramp)
            throw new InvalidOperationException($"Cannot set ramp direction on non-ramp terrain kind {kind}");

        if (direction > 7)
            throw new ArgumentException($"Invalid ramp direction {direction}, must be 0-7");

        // Clear direction bits and set new direction
        bits = (ushort)(bits & ~(RampDirMask << RampDirShift));
        bits |= (ushort)((direction & RampDirMask) << RampDirShift);

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
    /// Set smoothed flag.
    /// </summary>
    public static ushort SetSmoothed(ushort bits, bool smoothed)
    {
        if (smoothed)
            bits |= (ushort)(1 << SmoothedBit);
        else
            bits = (ushort)(bits & ~(1 << SmoothedBit));

        return bits;
    }

    /// <summary>
    /// Set engraved flag.
    /// </summary>
    public static ushort SetEngraved(ushort bits, bool engraved)
    {
        if (engraved)
            bits |= (ushort)(1 << EngravedBit);
        else
            bits = (ushort)(bits & ~(1 << EngravedBit));

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
    /// Check if approach direction is valid for ascending a ramp.
    /// Must approach from opposite direction (low side).
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