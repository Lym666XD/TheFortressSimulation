using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Navigation;

/// <summary>
/// Per-chunk navigation data per NAVIGATION_SPEC.md section 2.
/// Rebuilt during RebuildDerived phase in UPDATE_ORDER.
/// </summary>
public sealed class ChunkNavData
{
    public const int ChunkSize = 32;
    public const int TilesPerChunk = ChunkSize * ChunkSize;

    /// <summary>
    /// Navigation capability mask for each tile.
    /// Index = y * 32 + x.
    /// </summary>
    public byte[] NavMask { get; }

    /// <summary>
    /// Movement cost for each tile.
    /// Base cost + traffic + fluid + surface adjustments.
    /// </summary>
    public ushort[] NavCost { get; }

    /// <summary>
    /// Connectivity version for cache invalidation.
    /// Bumped when topology changes.
    /// </summary>
    public int ConnectivityVersion { get; set; }

    /// <summary>
    /// Chunk position in world.
    /// </summary>
    public ChunkKey Key { get; }

    public ChunkNavData(ChunkKey key)
    {
        Key = key;
        NavMask = new byte[TilesPerChunk];
        NavCost = new ushort[TilesPerChunk];
        ConnectivityVersion = 0;
    }

    /// <summary>
    /// Rebuild navigation data from tile information.
    /// Called during RebuildDerived phase.
    /// </summary>
    public void RebuildFromTiles(TileBase[] tiles, NavigationTuning tuning)
    {
        if (tiles.Length != TilesPerChunk)
            throw new ArgumentException($"Expected {TilesPerChunk} tiles, got {tiles.Length}");

        for (int idx = 0; idx < TilesPerChunk; idx++)
        {
            ref readonly var tile = ref tiles[idx];

            // Extract terrain kind from TerrainBits
            var terrainKind = (TerrainKind)(tile.TerrainBits & 0x7);
            var rampDir = (tile.TerrainBits >> 3) & 0x7;
            bool hasStairsUp = (tile.TerrainBits & (1 << 8)) != 0;
            bool hasStairsDown = (tile.TerrainBits & (1 << 9)) != 0;
            bool isChasm = (tile.TerrainBits & (1 << 10)) != 0;

            byte capabilities = 0;
            ushort cost = tuning.BaseCost;

            // Determine capabilities based on terrain
            switch (terrainKind)
            {
                case TerrainKind.OpenWithFloor:
                    capabilities |= (byte)(NavCapability.Walk | NavCapability.Standable);
                    break;

                case TerrainKind.Ramp:
                    // Ramps are walkable but direction-dependent
                    capabilities |= (byte)NavCapability.Walk;
                    cost += tuning.RampDelta;
                    break;

                case TerrainKind.StairsUp:
                case TerrainKind.StairsDown:
                case TerrainKind.StairsUD:
                    capabilities |= (byte)(NavCapability.Walk | NavCapability.Standable);
                    cost += tuning.StairDelta;
                    break;

                case TerrainKind.SolidWall:
                case TerrainKind.OpenNoFloor:
                case TerrainKind.Chasm:
                    // Not walkable
                    break;
            }

            // Apply fluid modifiers
            if (tile.FluidDepth > 0)
            {
                if (tile.FluidDepth <= tuning.FluidShallowThreshold)
                {
                    // Shallow fluid - walkable with cost
                    cost += tuning.FluidWadeCost;
                }
                else if (tile.FluidDepth >= tuning.FluidDeepThreshold)
                {
                    // Deep fluid - blocks walking unless swimming
                    capabilities &= (byte)~NavCapability.Walk;
                    capabilities |= (byte)NavCapability.Swim;
                    cost = tuning.FluidSwimCost;
                }
            }

            // Apply traffic modifiers (from MetaBits)
            var trafficLevel = (tile.MetaBits >> 4) & 0x3; // Assuming traffic in bits 4-5
            switch (trafficLevel)
            {
                case 1: // Low traffic
                    cost = (ushort)Math.Max(1, (int)cost + tuning.TrafficLow);
                    break;
                case 2: // High traffic
                    cost += (ushort)tuning.TrafficHigh;
                    break;
                case 3: // Restricted
                    cost += (ushort)tuning.TrafficRestricted;
                    break;
            }

            // Flying ignores most ground obstacles
            if (terrainKind != TerrainKind.SolidWall)
            {
                capabilities |= (byte)NavCapability.Fly;
            }

            NavMask[idx] = capabilities;
            NavCost[idx] = cost;
        }

        // Bump connectivity version after rebuild
        ConnectivityVersion++;
    }

    /// <summary>
    /// Check if a tile has specific capability.
    /// </summary>
    public bool HasCapability(int localIdx, NavCapability capability)
    {
        if (localIdx < 0 || localIdx >= TilesPerChunk)
            return false;
        return (NavMask[localIdx] & (byte)capability) != 0;
    }

    /// <summary>
    /// Get movement cost for a tile.
    /// </summary>
    public ushort GetCost(int localIdx)
    {
        if (localIdx < 0 || localIdx >= TilesPerChunk)
            return ushort.MaxValue;
        return NavCost[localIdx];
    }
}

/// <summary>
/// Terrain kinds from TILE_SPEC.md.
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
    Chasm = 7,
}