namespace HumanFortress.Simulation.Tiles;

/// <summary>
/// Base tile structure per TILE_SPEC.md.
/// Size: 10 bytes (may be padded by CLR).
/// Immutable for thread safety - use atomic replacement for updates.
/// </summary>
public readonly struct TileBase
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

    // Terrain bit accessors
    public TerrainKind Kind => (TerrainKind)(TerrainBits & 0x7);
    public RampDirection RampDir => (RampDirection)((TerrainBits >> 3) & 0x7);
    public bool IsNatural => (TerrainBits & (1 << 6)) != 0;
    public bool RequiresSupport => (TerrainBits & (1 << 7)) != 0;
    public bool HasStairUp => (TerrainBits & (1 << 8)) != 0;
    public bool HasStairDown => (TerrainBits & (1 << 9)) != 0;
    public bool IsChasm => (TerrainBits & (1 << 10)) != 0;

    // Surface bit accessors
    public bool HasMud => (SurfaceBits & 1) != 0;
    public bool HasGrass => (SurfaceBits & 2) != 0;
    public bool HasSnow => (SurfaceBits & 4) != 0;
    public bool HasAsh => (SurfaceBits & 8) != 0;
    public byte Fertility => (byte)(SurfaceBits >> 4);

    // Meta bit accessors
    public bool IsRevealed => (MetaBits & 1) != 0;
    public bool IsForbidden => (MetaBits & 2) != 0;
    public byte TrafficLevel => (byte)((MetaBits >> 2) & 0x3);
    public bool HasBlood => (MetaBits & 16) != 0;

    // Navigation helpers
    public bool IsPassable => Kind != TerrainKind.SolidWall;
    public bool HasFloor => Kind != TerrainKind.OpenNoFloor && Kind != TerrainKind.Chasm;
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
public enum TerrainKind : byte
{
    SolidWall = 0,
    OpenWithFloor = 1,
    OpenNoFloor = 2,
    Ramp = 3,
    StairsUp = 4,
    StairsDown = 5,
    StairsUD = 6,
    Chasm = 7
}

/// <summary>
/// Ramp directions.
/// </summary>
public enum RampDirection : byte
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
    Northeast = 4,
    Southwest = 5,
    Reserved6 = 6,
    None = 7
}