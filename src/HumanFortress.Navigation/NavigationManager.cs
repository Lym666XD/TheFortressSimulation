using System.Collections.Concurrent;

namespace HumanFortress.Navigation;

/// <summary>
/// Manages navigation data for all chunks.
/// Creates and updates ChunkNavData during RebuildDerived phase.
/// </summary>
public sealed class NavigationManager
{
    private readonly ConcurrentDictionary<ChunkKey, ChunkNavData> _navData;
    private readonly HumanFortress.Simulation.World.World _world;
    private readonly NavigationTuning _tuning;

    public NavigationManager(HumanFortress.Simulation.World.World world)
    {
        _world = world;
        _navData = new ConcurrentDictionary<ChunkKey, ChunkNavData>();
        _tuning = NavigationTuning.LoadFromContent();
    }

    /// <summary>
    /// Get or create navigation data for a chunk.
    /// </summary>
    public ChunkNavData GetOrCreateNavData(ChunkKey key)
    {
        return _navData.GetOrAdd(key, k => new ChunkNavData(k));
    }

    /// <summary>
    /// Get navigation data for a chunk if it exists.
    /// </summary>
    public ChunkNavData? GetNavData(ChunkKey key)
    {
        return _navData.GetValueOrDefault(key);
    }

    /// <summary>
    /// Get navigation data for a chunk if it exists (using Simulation ChunkKey).
    /// </summary>
    public ChunkNavData? GetNavData(HumanFortress.Simulation.World.ChunkKey simKey)
    {
        var navKey = new ChunkKey(simKey.ChunkX, simKey.ChunkY, simKey.Z);
        return GetNavData(navKey);
    }

    /// <summary>
    /// Get navigation data at world coordinates.
    /// </summary>
    public ChunkNavData? GetNavDataAt(int worldX, int worldY, int z)
    {
        var chunkX = worldX / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        var chunkY = worldY / HumanFortress.Simulation.World.Chunk.SIZE_XY;
        return GetNavData(new ChunkKey(chunkX, chunkY, z));
    }

    /// <summary>
    /// Rebuild navigation data for a chunk (during RebuildDerived phase).
    /// </summary>
    public void RebuildChunkNavData(HumanFortress.Simulation.World.Chunk chunk)
    {
        var navKey = new ChunkKey(chunk.Key.ChunkX, chunk.Key.ChunkY, chunk.Key.Z);
        var navData = GetOrCreateNavData(navKey);
        var tiles = chunk.GetTilesCopy();
        navData.RebuildFromTiles(tiles, _tuning);

        // Precompute ramp connectivity flags for O(1) neighbor expansion
        // UpRamp: current tile is ramp base, points to top at z+1
        // DownRamp: current tile is standable top and has a matching ramp below/behind at z-1
        int baseWorldX = chunk.Key.ChunkX * HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int baseWorldY = chunk.Key.ChunkY * HumanFortress.Simulation.World.Chunk.SIZE_XY;
        int z = chunk.Key.Z;

        for (int ly = 0; ly < ChunkNavData.ChunkSize; ly++)
        {
            for (int lx = 0; lx < ChunkNavData.ChunkSize; lx++)
            {
                int idx = ly * ChunkNavData.ChunkSize + lx;
                ref readonly var tile = ref tiles[idx];

                // Reset flags
                navData.UpRampDir[idx] = 255;
                navData.DownRampDir[idx] = 255;
                // Clear reserved bits 6 and 7
                navData.NavMask[idx] = (byte)(navData.NavMask[idx] & 0b0011_1111);

                // Up ramp (base -> top)
                if (tile.Kind == HumanFortress.Simulation.Tiles.TerrainKind.Ramp)
                {
                    byte dir = tile.RampDir;
                    var (dx, dy) = HumanFortress.Simulation.Tiles.TerrainBitOps.GetDirectionOffset(dir);
                    int topX = baseWorldX + lx + dx;
                    int topY = baseWorldY + ly + dy;
                    int topZ = z + 1;

                    var topTile = _world.GetTile(topX, topY, topZ);
                    if (topTile.HasValue && (topTile.Value.Kind == HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor || topTile.Value.Kind == HumanFortress.Simulation.Tiles.TerrainKind.Slope))
                    {
                        navData.UpRampDir[idx] = dir;
                        // Set bit6 for UpRamp
                        navData.NavMask[idx] = (byte)(navData.NavMask[idx] | (1 << 6));
                    }
                }

                // Down ramp (top -> base behind)
                if (tile.Kind == HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor || tile.Kind == HumanFortress.Simulation.Tiles.TerrainKind.Slope)
                {
                    for (byte dir = 0; dir < 8; dir++)
                    {
                        var (dx, dy) = HumanFortress.Simulation.Tiles.TerrainBitOps.GetDirectionOffset(dir);
                        int rampX = baseWorldX + lx - dx;
                        int rampY = baseWorldY + ly - dy;
                        int rampZ = z - 1;

                        var rampTile = _world.GetTile(rampX, rampY, rampZ);
                        if (rampTile.HasValue && rampTile.Value.Kind == HumanFortress.Simulation.Tiles.TerrainKind.Ramp && rampTile.Value.RampDir == dir)
                        {
                            navData.DownRampDir[idx] = dir;
                            // Set bit7 for DownRamp
                            navData.NavMask[idx] = (byte)(navData.NavMask[idx] | (1 << 7));
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Rebuild all navigation data.
    /// </summary>
    public void RebuildAll()
    {
        foreach (var chunk in _world.GetAllChunks())
        {
            RebuildChunkNavData(chunk);
        }
    }
}
