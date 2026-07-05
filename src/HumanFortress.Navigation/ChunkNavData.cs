using HumanFortress.Contracts.Navigation;

namespace HumanFortress.Navigation.Implementation;

/// <summary>
/// Per-chunk navigation data per NAVIGATION_SPEC.md section 2.
/// Rebuilt during RebuildDerived phase in UPDATE_ORDER.
/// </summary>
internal sealed class ChunkNavData
{
    internal const int ChunkSize = 32;
    internal const int TilesPerChunk = ChunkSize * ChunkSize;

    /// <summary>
    /// Navigation capability mask for each tile.
    /// Index = y * 32 + x.
    /// </summary>
    internal byte[] NavMask { get; }

    /// <summary>
    /// Movement cost for each tile.
    /// Base cost + traffic + fluid + surface adjustments.
    /// </summary>
    internal ushort[] NavCost { get; }

    /// <summary>
    /// Per-tile mask of allowed ascend directions for ramps.
    /// Bits 0..7 correspond to N,NE,E,SE,S,SW,W,NW. 0 = no ascend from this tile.
    /// </summary>
    internal byte[] UpRampMask { get; }

    /// <summary>
    /// Connectivity version for cache invalidation.
    /// Bumped when topology changes.
    /// </summary>
    internal int ConnectivityVersion { get; set; }

    /// <summary>
    /// Snapshot of the source chunk's connectivity version that this nav was built from.
    /// Used to detect staleness vs the authoritative navigation source.
    /// </summary>
    internal ulong SourceConnectivityVersion { get; set; }

    /// <summary>
    /// Chunk position in world.
    /// </summary>
    internal ChunkKey Key { get; }

    internal ChunkNavData(ChunkKey key)
    {
        Key = key;
        NavMask = new byte[TilesPerChunk];
        NavCost = new ushort[TilesPerChunk];
        UpRampMask = new byte[TilesPerChunk];
        ConnectivityVersion = 0;
        SourceConnectivityVersion = 0;
    }

    /// <summary>
    /// Rebuild navigation data from tile information.
    /// Called during RebuildDerived phase.
    /// </summary>
    internal void RebuildFromTiles(NavigationTile[] tiles, NavigationTuning tuning)
    {
        if (tiles.Length != TilesPerChunk)
            throw new ArgumentException($"Expected {TilesPerChunk} tiles, got {tiles.Length}");

        for (int idx = 0; idx < TilesPerChunk; idx++)
        {
            ref readonly var tile = ref tiles[idx];

            byte capabilities = 0;
            ushort cost = tuning.BaseCost;

            // NavigationTile owns legality decisions; terrain-specific details stay behind adapters.
            if (tile.IsWalkable)
            {
                capabilities |= (byte)NavCapability.Walk;
            }

            if (tile.IsStandable)
            {
                capabilities |= (byte)NavCapability.Standable;
            }

            if (tile.IsFlyable)
            {
                capabilities |= (byte)NavCapability.Fly;
            }

            switch (tile.Kind)
            {
                case NavigationTileKind.Ramp:
                    cost += tuning.RampDelta;
                    break;
                case NavigationTileKind.StairsUp:
                case NavigationTileKind.StairsDown:
                case NavigationTileKind.StairsUD:
                    cost += tuning.StairDelta;
                    break;
            }

            if (tile.FluidDepth > 0)
            {
                if (tile.FluidDepth <= tuning.FluidShallowThreshold)
                {
                    cost += tuning.FluidWadeCost;
                }
                else if (tile.FluidDepth >= tuning.FluidDeepThreshold)
                {
                    capabilities &= (byte)~NavCapability.Walk;
                    capabilities |= (byte)NavCapability.Swim;
                    cost = tuning.FluidSwimCost;
                }
            }

            var trafficLevel = (tile.MetaBits >> 4) & 0x3; // Traffic in bits 4-5.
            switch (trafficLevel)
            {
                case 1:
                    cost = (ushort)Math.Max(1, (int)cost + tuning.TrafficLow);
                    break;
                case 2:
                    cost += (ushort)tuning.TrafficHigh;
                    break;
                case 3:
                    cost += (ushort)tuning.TrafficRestricted;
                    break;
            }

            NavMask[idx] = capabilities;
            NavCost[idx] = cost;
        }

        ConnectivityVersion++;
    }

    /// <summary>
    /// Check if a tile has specific capability.
    /// </summary>
    internal bool HasCapability(int localIdx, NavCapability capability)
    {
        if (localIdx < 0 || localIdx >= TilesPerChunk)
            return false;

        return (NavMask[localIdx] & (byte)capability) != 0;
    }

    /// <summary>
    /// Get movement cost for a tile.
    /// </summary>
    internal ushort GetCost(int localIdx)
    {
        if (localIdx < 0 || localIdx >= TilesPerChunk)
            return ushort.MaxValue;

        return NavCost[localIdx];
    }
}
