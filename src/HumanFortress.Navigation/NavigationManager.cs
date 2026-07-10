using HumanFortress.Contracts.Navigation;

namespace HumanFortress.Navigation.Implementation;

/// <summary>
/// Manages navigation data for all chunks.
/// Creates and updates ChunkNavData during RebuildDerived phase.
/// </summary>
internal sealed class NavigationManager
{
    private readonly Dictionary<ChunkKey, ChunkNavData> _navData;
    private readonly INavigationWorldSource _source;
    private readonly NavigationTuning _tuning;
    private readonly object _sync = new();

    /// <summary>
    /// Optional logging callback (set by App layer to write to fortress_debug.log)
    /// </summary>
    internal static Action<string>? LogCallback { get; set; }

    internal NavigationManager(INavigationWorldSource source, NavigationTuning? tuning = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _navData = new Dictionary<ChunkKey, ChunkNavData>();
        _tuning = tuning ?? NavigationTuning.Default;
    }

    internal INavigationWorldSource Source => _source;

    /// <summary>
    /// Get or create navigation data for a chunk.
    /// </summary>
    internal ChunkNavData GetOrCreateNavData(ChunkKey key)
    {
        lock (_sync)
        {
            if (!_navData.TryGetValue(key, out var navData))
            {
                navData = new ChunkNavData(key);
                _navData.Add(key, navData);
            }

            return navData;
        }
    }

    /// <summary>
    /// Get navigation data for a chunk if it exists.
    /// </summary>
    internal ChunkNavData? GetNavData(ChunkKey key)
    {
        lock (_sync)
        {
            return _navData.GetValueOrDefault(key);
        }
    }

    /// <summary>
    /// Get navigation data at world coordinates.
    /// </summary>
    internal ChunkNavData? GetNavDataAt(int worldX, int worldY, int z)
    {
        var chunkX = worldX / ChunkNavData.ChunkSize;
        var chunkY = worldY / ChunkNavData.ChunkSize;
        var key = new ChunkKey(chunkX, chunkY, z);
        return GetNavData(key);
    }

    /// <summary>
    /// Rebuild navigation data for a chunk (during RebuildDerived phase).
    /// </summary>
    internal void RebuildChunkNavData(ChunkKey key)
    {
        if (_source.TryGetChunk(key, out var chunk))
        {
            RebuildChunkNavData(chunk);
        }
    }

    /// <summary>
    /// Rebuild navigation data for a source chunk snapshot.
    /// </summary>
    internal void RebuildChunkNavData(NavigationChunkSnapshot chunk)
    {
        var navData = GetOrCreateNavData(chunk.Key);
        var tiles = chunk.Tiles;
        var oldVersion = navData.ConnectivityVersion;
        navData.RebuildFromTiles(tiles, _tuning);
        navData.SourceConnectivityVersion = chunk.ConnectivityVersion;
        var newVersion = navData.ConnectivityVersion;
        LogCallback?.Invoke($"[NAV] RebuildChunkNavData chunk=({chunk.Key.ChunkX},{chunk.Key.ChunkY},{chunk.Key.Z}) version:{oldVersion}→{newVersion}");

        // Precompute ramp connectivity flags for O(1) neighbor expansion
        // UpRampMask: for ramp base at (x,y,z), bits 0..7 allow ascend to standable tiles at z+1 around (x,y)
        int baseWorldX = chunk.Key.ChunkX * ChunkNavData.ChunkSize;
        int baseWorldY = chunk.Key.ChunkY * ChunkNavData.ChunkSize;
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
                if (tile.Kind == NavigationTileKind.Ramp)
                {
                    // DF-style: top space above base must be OpenNoFloor
                    if (_source.TryGetTile(new Point3(baseWorldX + lx, baseWorldY + ly, z + 1), out var topSpace)
                        && topSpace.Kind == NavigationTileKind.OpenNoFloor)
                    {
                        byte mask = 0;
                        for (byte dir = 0; dir < 8; dir++)
                        {
                            var (dx, dy) = GetDirectionOffset(dir);
                            int topX = baseWorldX + lx + dx;
                            int topY = baseWorldY + ly + dy;
                            int topZ = z + 1;

                            if (!_source.TryGetTile(new Point3(topX, topY, topZ), out var topTile)) continue;

                            // Must be standable (floor/slope) at z+1
                            if (!topTile.IsStandable) continue;

                            // High-side support rule (below top at z)
                            if (_tuning.RampRequiresHighsideSupport)
                            {
                                if (!_source.TryGetTile(new Point3(topX, topY, z), out var highSide)
                                    || highSide.Kind != NavigationTileKind.SolidWall)
                                {
                                    continue;
                                }
                            }

                            // Diagonal corner check for ramp ascend (relaxed: require at least one adjacent orth standable at z+1)
                            if (_tuning.DiagonalCornerCheck && (dx != 0 && dy != 0))
                            {
                                bool o1 = _source.TryGetTile(new Point3(baseWorldX + lx + dx, baseWorldY + ly, topZ), out var ortho1)
                                    && ortho1.IsStandable;
                                bool o2 = _source.TryGetTile(new Point3(baseWorldX + lx, baseWorldY + ly + dy, topZ), out var ortho2)
                                    && ortho2.IsStandable;
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
    internal void RebuildAll()
    {
        foreach (var chunk in _source.GetAllChunks())
        {
            RebuildChunkNavData(chunk);
        }
    }

    private static (int dx, int dy) GetDirectionOffset(byte dir)
    {
        return dir switch
        {
            0 => (0, -1),
            1 => (1, -1),
            2 => (1, 0),
            3 => (1, 1),
            4 => (0, 1),
            5 => (-1, 1),
            6 => (-1, 0),
            7 => (-1, -1),
            _ => (0, 0),
        };
    }
}
