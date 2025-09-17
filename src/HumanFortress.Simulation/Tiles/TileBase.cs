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

    // Terrain bit accessors per canonical layout
    // bits 0-2: TerrainKind (0-7)
    // bits 3-5: RampDirection (0-7, valid only when Kind=Ramp)
    // bit 6: Natural (1=natural, 0=constructed)
    // bit 7: Smoothed (optional finish state)
    // bit 8: Engraved (optional finish state)
    // bits 9-15: Reserved (must be 0)
    public TerrainKind Kind => (TerrainKind)(TerrainBits & 0x7);
    public byte RampDir => (byte)((TerrainBits >> 3) & 0x7);
    public bool IsNatural => (TerrainBits & (1 << 6)) != 0;
    public bool IsSmoothed => (TerrainBits & (1 << 7)) != 0;
    public bool IsEngraved => (TerrainBits & (1 << 8)) != 0;

    // Ramp direction is only valid when Kind == Ramp
    public bool HasValidRampDirection => Kind == TerrainKind.Ramp;
    public RampDirection GetRampDirection() => HasValidRampDirection ? (RampDirection)RampDir : RampDirection.North;

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

    // Navigation helpers based on TerrainKind only
    public bool IsWalkable => Kind switch
    {
        TerrainKind.OpenWithFloor => true,
        TerrainKind.Ramp => true,
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
public enum TerrainKind : byte
{
    SolidWall = 0,      // Blocks all movement, provides support
    OpenWithFloor = 1,  // Walkable floor, provides support
    OpenNoFloor = 2,    // Empty space, flyable only, no support
    Ramp = 3,           // Z-transition using RampDirection bits
    StairsUp = 4,       // Z-transition up only
    StairsDown = 5,     // Z-transition down only
    StairsUD = 6,       // Z-transition both ways
    Chasm = 7           // Bottomless pit, flyable only, no support
}

/// <summary>
/// Ramp directions.
/// </summary>
public enum RampDirection : byte
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