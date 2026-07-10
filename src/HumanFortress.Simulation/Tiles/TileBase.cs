namespace HumanFortress.Simulation.Tiles;

/// <summary>
/// Base tile structure per TILE_SPEC.md.
/// Size: 10 bytes (may be padded by CLR).
/// Immutable for thread safety - use atomic replacement for updates.
/// </summary>
internal readonly struct TileBase
{
    internal readonly ushort GeoMatId;     // L0: geology/terrain material (IdMap index)
    internal readonly ushort TerrainBits;  // L0: kind/flags (bit layout below)
    internal readonly byte SurfaceBits;    // L1: small flags (mud, grass, fertility)
    internal readonly byte FluidKind;      // L3: fluid IdMap index (0 = none)
    internal readonly byte FluidDepth;     // L3: 0..7
    internal readonly byte MetaBits;       // L7: revealed/traffic/etc.
    internal readonly ushort TrafficCost;  // Cached nav cost

    internal TileBase(
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
    internal TerrainKind Kind => (TerrainKind)(TerrainBits & 0xF);
    internal bool IsNatural => (TerrainBits & (1 << 5)) != 0;
    internal bool IsModifiable => (TerrainBits & (1 << 6)) != 0;

    // Surface bit accessors
    // bit0: Mud, bit1: Grass, bit2: Snow, bit3: Moss (cavern), bits4..7 Fertility
    internal bool HasMud => (SurfaceBits & 1) != 0;
    internal bool HasGrass => (SurfaceBits & 2) != 0;
    internal bool HasSnow => (SurfaceBits & 4) != 0;
    internal bool HasMoss => (SurfaceBits & 8) != 0;
    internal byte Fertility => (byte)(SurfaceBits >> 4);

    // Meta bit accessors
    internal bool IsRevealed => (MetaBits & 1) != 0;
    internal bool IsForbidden => (MetaBits & 2) != 0;
    internal byte TrafficLevel => (byte)((MetaBits >> 2) & 0x3);
    internal bool HasBlood => (MetaBits & 16) != 0;

    // Navigation helpers based on TerrainKind only
    internal bool IsWalkable => Kind switch
    {
        TerrainKind.OpenWithFloor => true,
        TerrainKind.Ramp => true,
        TerrainKind.Slope => true,
        TerrainKind.StairsUp => true,
        TerrainKind.StairsDown => true,
        TerrainKind.StairsUD => true,
        _ => false
    };

    internal bool IsStandable => Kind == TerrainKind.OpenWithFloor;

    internal bool IsFlyable => Kind != TerrainKind.SolidWall;

    internal bool ProvidesSupport => Kind == TerrainKind.SolidWall || Kind == TerrainKind.OpenWithFloor;

    internal bool BlocksLOS => Kind == TerrainKind.SolidWall;

    /// <summary>
    /// Create a modified copy with new terrain bits.
    /// </summary>
    internal TileBase WithTerrain(ushort newTerrainBits)
    {
        return new TileBase(GeoMatId, newTerrainBits, SurfaceBits,
            FluidKind, FluidDepth, MetaBits, TrafficCost);
    }

    /// <summary>
    /// Create a modified copy with new fluid.
    /// </summary>
    internal TileBase WithFluid(byte kind, byte depth)
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
