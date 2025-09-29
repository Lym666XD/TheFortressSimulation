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
        // UpRampMask: for ramp base at (x,y,z), bits 0..7 allow ascend to standable tiles at z+1 around (x,y)
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
                navData.UpRampMask[idx] = 0;
                // Clear reserved bits 6 and 7
                navData.NavMask[idx] = (byte)(navData.NavMask[idx] & 0b0011_1111);

                // Up ramp mask (base -> any allowed tops around)
                if (tile.Kind == HumanFortress.Simulation.Tiles.TerrainKind.Ramp)
                {
                    // DF-style: top space above base must be OpenNoFloor
                    var topSpace = _world.GetTile(baseWorldX + lx, baseWorldY + ly, z + 1);
                    if (topSpace.HasValue && topSpace.Value.Kind == HumanFortress.Simulation.Tiles.TerrainKind.OpenNoFloor)
                    {
                        byte mask = 0;
                        for (byte dir = 0; dir < 8; dir++)
                        {
                            var (dx, dy) = HumanFortress.Simulation.Tiles.TerrainBitOps.GetDirectionOffset(dir);
                            int topX = baseWorldX + lx + dx;
                            int topY = baseWorldY + ly + dy;
                            int topZ = z + 1;

                            var topTile = _world.GetTile(topX, topY, topZ);
                            if (!topTile.HasValue) continue;

                            // Must be standable (floor/slope) at z+1
                            if (!(topTile.Value.IsStandable)) continue;

                            // High-side support rule (below top at z)
                            if (_tuning.RampRequiresHighsideSupport)
                            {
                                var highSide = _world.GetTile(topX, topY, z);
                                if (!highSide.HasValue || highSide.Value.Kind != HumanFortress.Simulation.Tiles.TerrainKind.SolidWall)
                                {
                                    continue;
                                }
                            }

                            // Diagonal corner check for ramp ascend (relaxed: require at least one adjacent orth standable at z+1)
                            if (_tuning.DiagonalCornerCheck && (dx != 0 && dy != 0))
                            {
                                var ortho1 = _world.GetTile(baseWorldX + lx + dx, baseWorldY + ly, topZ);
                                var ortho2 = _world.GetTile(baseWorldX + lx, baseWorldY + ly + dy, topZ);
                                bool o1 = ortho1.HasValue && ortho1.Value.IsStandable;
                                bool o2 = ortho2.HasValue && ortho2.Value.IsStandable;
                                if (!(o1 || o2))
                                {
                                    continue;
                                }
                            }

                            // All checks passed; allow this direction
                            mask |= (byte)(1 << dir);
                        }

                        if (mask != 0)
                        {
                            navData.UpRampMask[idx] = mask;
                            // Set bit6 for having any up ramp connectivity
                            navData.NavMask[idx] = (byte)(navData.NavMask[idx] | (1 << 6));
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
