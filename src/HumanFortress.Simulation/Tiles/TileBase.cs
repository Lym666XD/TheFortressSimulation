namespace HumanFortress.Simulation.Tiles;

/// <summary>
/// Base tile structure per TILE_SPEC.md.
/// Size: 10 bytes (may be padded by CLR).
/// Immutable for thread safety - use atomic replacement for updates.
/// </summary>
internal readonly struct TileBase
{
    public readonly ushort GeoMatId;     // L0: geology/terrain material (IdMap index)
    public readonly ushort TerrainBits;  // L0: kind/flags (bit layout below)
    public readonly byte SurfaceBits;    // L1: small flags (mud, grass, fertility)
    public readonly byte FluidKind;      // L3: fluid IdMap index (0 = none)
    public readonly byte FluidDepth;     // L3: 0..7
    public readonly byte MetaBits;       // L7: revealed/traffic/etc.
    public readonly ushort TrafficCost;  // Cached nav cost

    public TileBase(
        ushort geoMatId,
        ushort terrainBits,
        byte surfaceBits,
        byte fluidKind,
        byte fluidDepth,
        byte metaBits,
        ushort trafficCost)
    {
        GeoMatId = geoMatId;
        TerrainBits = terrainBits;
        SurfaceBits = surfaceBits;
        FluidKind = fluidKind;
        FluidDepth = fluidDepth;
        MetaBits = metaBits;
        TrafficCost = trafficCost;
    }

    // Terrain bit accessors per canonical layout (v2)
    // bits 0-3: TerrainKind (0-15)
    // bit 5: Natural (1=natural, 0=constructed)
    // bit 6: Modifiable (1=player tools allowed; 0=forbidden)
    // other bits reserved (must be 0)
    public TerrainKind Kind => (TerrainKind)(TerrainBits & 0xF);
    public bool IsNatural => (TerrainBits & (1 << 5)) != 0;
    public bool IsModifiable => (TerrainBits & (1 << 6)) != 0;

    // Surface bit accessors
    // bit0: Mud, bit1: Grass, bit2: Snow, bit3: Moss (cavern), bits4..7 Fertility
    public bool HasMud => (SurfaceBits & 1) != 0;
    public bool HasGrass => (SurfaceBits & 2) != 0;
    public bool HasSnow => (SurfaceBits & 4) != 0;
    public bool HasMoss => (SurfaceBits & 8) != 0;
    public byte Fertility => (byte)(SurfaceBits >> 4);

    // Meta bit accessors
    public bool IsRevealed => (MetaBits & 1) != 0;
    public bool IsForbidden => (MetaBits & 2) != 0;
    public byte TrafficLevel => (byte)((MetaBits >> 2) & 0x3);
    public bool HasBlood => (MetaBits & 16) != 0;

    // Navigation helpers based on TerrainKind only
    public bool IsWalkable => Kind switch
    {
        TerrainKind.OpenWithFloor => true,
        TerrainKind.Ramp => true,
        TerrainKind.Slope => true,
        TerrainKind.StairsUp => true,
        TerrainKind.StairsDown => true,
        TerrainKind.StairsUD => true,
        _ => false
    };

    public bool IsStandable => Kind == TerrainKind.OpenWithFloor;

    public bool IsFlyable => Kind != TerrainKind.SolidWall;

    public bool ProvidesSupport => Kind == TerrainKind.SolidWall || Kind == TerrainKind.OpenWithFloor;

    public bool BlocksLOS => Kind == TerrainKind.SolidWall;

    /// <summary>
    /// Create a modified copy with new terrain bits.
    /// </summary>
    public TileBase WithTerrain(ushort newTerrainBits)
    {
        return new TileBase(GeoMatId, newTerrainBits, SurfaceBits,
            FluidKind, FluidDepth, MetaBits, TrafficCost);
    }

    /// <summary>
    /// Create a modified copy with new fluid.
    /// </summary>
    public TileBase WithFluid(byte kind, byte depth)
    {
        return new TileBase(GeoMatId, TerrainBits, SurfaceBits,
            kind, depth, MetaBits, TrafficCost);
    }
}

/// <summary>
/// Terrain types per TILE_SPEC.md.
/// </summary>
internal enum TerrainKind : byte
{
    SolidWall = 0,      // Blocks all movement, provides support
    OpenWithFloor = 1,  // Walkable floor, provides support
    OpenNoFloor = 2,    // Empty space, flyable only, no support
    Ramp = 3,           // Z-transition using RampDirection bits
    Slope = 4,          // Walkable slope top (paired with a Ramp below)
    StairsUp = 5,       // Z-transition up only
    StairsDown = 6,     // Z-transition down only
    StairsUD = 7        // Z-transition both ways
}

/// <summary>
/// Ramp directions helper enum retained for offsets.
/// </summary>
internal enum RampDirection : byte
{
    North = 0,
    Northeast = 1,
    East = 2,
    Southeast = 3,
    South = 4,
    Southwest = 5,
    West = 6,
    Northwest = 7
}
