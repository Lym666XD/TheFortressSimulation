using System;
using System.Collections.Generic;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Core.Diagnostics;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Tiles;

namespace HumanFortress.WorldGen
{
    using TerrainKind = HumanFortress.Simulation.Tiles.TerrainKind;
    /// <summary>
    /// Represents the generated fortress map data.
    /// Uses geology handles directly instead of TerrainType enum.
    /// </summary>
    internal sealed class FortressMap
    {
        private readonly FortressChunk[,] _chunks;
        private readonly IRuntimeGeologyCatalog _geology;
        private readonly int _size;
        private readonly int _maxZ;
        
        public int Size => _size;
        public int MaxZ => _maxZ;
        
        public FortressMap(int size, int maxZ, IRuntimeGeologyCatalog geology)
        {
            _geology = geology ?? throw new ArgumentNullException(nameof(geology));
            _size = size;
            _maxZ = maxZ;
            _chunks = new FortressChunk[size, size];
            
            // Initialize chunks
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    _chunks[x, y] = new FortressChunk(x, y, maxZ, _geology);
                }
            }
        }
        
        public FortressChunk GetChunk(int x, int y)
        {
            if (x < 0 || x >= _size || y < 0 || y >= _size)
                throw new ArgumentOutOfRangeException($"Chunk coordinates {x},{y} out of range");
            
            return _chunks[x, y];
        }
        
        /// <summary>
        /// Fill an existing World with terrain data from this fortress map.
        /// This is the preferred method - it fills terrain into a World that already has
        /// CreatureManager and ItemManager with loaded definitions.
        /// </summary>
        public void FillWorld(World targetWorld)
        {
            try
            {
                Emit($"[FillWorld] Filling world with terrain data: {_size}x{_size} chunks, MaxZ={_maxZ}");

                if (targetWorld == null)
                    throw new ArgumentNullException(nameof(targetWorld));

                if (targetWorld.SizeInChunks != _size)
                    throw new ArgumentException($"World size mismatch: expected {_size}, got {targetWorld.SizeInChunks}");

                if (targetWorld.MaxZ != _maxZ)
                    throw new ArgumentException($"World MaxZ mismatch: expected {_maxZ}, got {targetWorld.MaxZ}");

                int chunksProcessed = 0;
                int tilesProcessed = 0;

                // Transfer terrain data to existing world
                for (int cx = 0; cx < _size; cx++)
                {
                    for (int cy = 0; cy < _size; cy++)
                    {
                        var fortressChunk = _chunks[cx, cy];
                        if (fortressChunk == null)
                        {
                            Emit($"[FillWorld] WARNING: Null chunk at {cx},{cy}");
                            continue;
                        }

                        // Create chunks for each Z level that has content
                        for (int z = 0; z < _maxZ; z++)
                        {
                            var chunkKey = new ChunkKey(cx, cy, z);
                            var simChunk = targetWorld.GetOrCreateChunk(chunkKey);

                            // Copy terrain data
                            for (int lx = 0; lx < 32; lx++)
                            {
                                for (int ly = 0; ly < 32; ly++)
                                {
                                    var geologyHandle = fortressChunk.GetGeologyHandle(lx, ly, z);
                                    var surfaceBits = fortressChunk.GetSurfaceBits(lx, ly, z);
                                    var tile = ConvertGeologyToTile(geologyHandle, surfaceBits, z);
                                    simChunk.SetTile(lx, ly, tile, 0);
                                    tilesProcessed++;
                                }
                            }
                        }
                        chunksProcessed++;
                    }
                }

                Emit($"[FillWorld] Filled {chunksProcessed} chunks, {tilesProcessed} tiles");

                // Post-process: inject ramps based on surface height differences (DF-style)
                Emit("[FillWorld] Post-processing ramps (DF-style, no slope tops)");
                InjectRampsAndSlopes(targetWorld);

                Emit("[FillWorld] World terrain filling complete");
            }
            catch (Exception ex)
            {
                Emit($"[FillWorld] ERROR: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Convert to World for simulation.
        /// OBSOLETE: Use FillWorld(World) instead to preserve loaded Creature/Item definitions.
        /// This method is kept for backward compatibility with tests.
        /// </summary>
        [Obsolete("Use FillWorld(World) instead to preserve loaded definitions")]
        public World ToSimulationWorld()
        {
            try
            {
                Emit($"[ToSimulationWorld] Converting fortress map to world: {_size}x{_size} chunks, MaxZ={_maxZ}");
                Emit("[ToSimulationWorld] WARNING: This method creates a new World with empty managers. Use FillWorld(World) instead.");

                var world = new World(_size, _maxZ);
                FillWorld(world);
                return world;
            }
            catch (Exception ex)
            {
                Emit($"[ToSimulationWorld] ERROR: {ex.Message}", ex);
                throw;
            }
        }

        private static void Emit(string message)
        {
            DiagnosticHub.Sink.Information("WorldGen.FortressMap", message);
            if (!DiagnosticHub.IsConfigured)
            {
                System.Console.WriteLine(message);
            }
        }

        private static void Emit(string message, Exception exception)
        {
            DiagnosticHub.Sink.Error("WorldGen.FortressMap", message, exception);
            if (!DiagnosticHub.IsConfigured)
            {
                System.Console.WriteLine(message);
            }
        }

        private void InjectRampsAndSlopes(World world)
        {
            int tiles = world.SizeInTiles;
            int maxZ = world.MaxZ;
            var surfZ = new int[tiles, tiles];
            for (int y = 0; y < tiles; y++)
            {
                for (int x = 0; x < tiles; x++)
                {
                    int zTop = -1;
                    for (int z = maxZ - 1; z >= 0; z--)
                    {
                        var t = world.GetTile(x, y, z);
                        if (t.HasValue && (t.Value.Kind == TerrainKind.OpenWithFloor || t.Value.Kind == TerrainKind.Slope))
                        {
                            zTop = z;
                            break;
                        }
                    }
                    surfZ[x, y] = zTop;
                }
            }

            // NESW deterministic order
            // 8-neighborhood (N,NE,E,SE,S,SW,W,NW) to detect any rising neighbor at z+1
            var dirs = new (int dx, int dy, byte dirCode)[]
            {
                (0, -1, 0), // N
                (1, -1, 1), // NE
                (1, 0, 2),  // E
                (1, 1, 3),  // SE
                (0, 1, 4),  // S
                (-1, 1, 5), // SW
                (-1, 0, 6), // W
                (-1, -1, 7),// NW
            };

            for (int y = 0; y < tiles; y++)
            {
                for (int x = 0; x < tiles; x++)
                {
                    int s = surfZ[x, y];
                    if (s < 0) continue;
                    var cur = world.GetTile(x, y, s);
                    if (!cur.HasValue) continue;

                    // Only convert from plain floor
                    if (!(cur.Value.Kind == TerrainKind.OpenWithFloor))
                        continue;

                    foreach (var (dx, dy, code) in dirs)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= tiles || ny >= tiles) continue;
                        int ns = surfZ[nx, ny];
                        if (ns == s + 1)
                        {
                            // Set current as Ramp base (DF-style)
                            var curTile = cur.Value;
                            ushort bits = curTile.TerrainBits;
                            bits = TerrainBitOps.SetKind(bits, TerrainKind.Ramp);
                            var rampTile = curTile.WithTerrain(bits);
                            world.SetTile(x, y, s, rampTile, 0);

                            // DF-style: ensure the cell directly above the ramp base is open air (OpenNoFloor)
                            // This clears any leftover ceiling so vertical alignment is unblocked.
                            var aboveOpt = world.GetTile(x, y, s + 1);
                            if (aboveOpt.HasValue)
                            {
                                var above = aboveOpt.Value;
                                ushort aboveBits = above.TerrainBits;
                                aboveBits = TerrainBitOps.SetKind(aboveBits, TerrainKind.OpenNoFloor);
                                var airTile = above.WithTerrain(aboveBits).WithFluid(0, 0);
                                world.SetTile(x, y, s + 1, airTile, 0);
                            }

                            // DF-style: do not inject slope geometry at z+1; top remains standable floor

                            break; // only one ramp direction per tile
                        }
                    }
                }
            }
        }

        private TileBase ConvertGeologyToTile(ushort geologyHandle, byte surfaceBits, int z)
        {
            var geology = _geology.GetGeologyByHandle(geologyHandle);
            if (geology == null)
            {
                // Fallback to a solid rock wall if geology not found
                return new TileBase(
                    geoMatId: _geology.GetGeologyHandle("core_terrain_wall_rock_granite"),
                    terrainBits: (ushort)TerrainKind.SolidWall,
                    surfaceBits: surfaceBits,
                    fluidKind: 0,
                    fluidDepth: 0,
                    metaBits: 0,
                    trafficCost: 10
                );
            }

            // Parse TerrainKind from string
            var terrainKind = Enum.TryParse<TerrainKind>(geology.TerrainBits.Kind, out var kind)
                ? kind
                : TerrainKind.OpenNoFloor;

            // Create TerrainBits with Natural/Modifiable (modifiable false only for bottommost z=0)
            bool natural = geology.TerrainBits.Natural;
            bool modifiable = z > 0; // bottommost layer z==0 is not modifiable
            var terrainBits = TerrainBitOps.CreateTerrainBits(
                terrainKind,
                natural,
                modifiable
            );

            return new TileBase(
                geoMatId: geologyHandle,
                terrainBits: terrainBits,
                surfaceBits: surfaceBits,
                fluidKind: 0,
                fluidDepth: 0,
                metaBits: 0,
                trafficCost: (ushort)(geology.Properties?.NavCostBase ?? 100)
            );
        }

        // Removed ramp direction parsing/inference: DF-style ramps derive directions at nav rebuild time

        private TerrainKind GetGeologyKindGlobal(int gx, int gy, int z)
        {
            int cx = gx / 32;
            int cy = gy / 32;
            int lx = gx % 32;
            int ly = gy % 32;
            if (cx < 0 || cy < 0 || cx >= _size || cy >= _size || z < 0 || z >= _maxZ)
                return TerrainKind.SolidWall;

            var chunk = _chunks[cx, cy];
            var handle = chunk.GetGeologyHandle(lx, ly, z);
            var geo = _geology.GetGeologyByHandle(handle);
            if (geo == null) return TerrainKind.OpenNoFloor;
            return Enum.TryParse<TerrainKind>(geo.TerrainBits.Kind, out var kind) ? kind : TerrainKind.OpenNoFloor;
        }
    }
    
    /// <summary>
    /// Represents a single chunk in the fortress map.
    /// Now stores geology handles directly instead of TerrainType enum.
    /// </summary>
    internal sealed class FortressChunk
    {
        private readonly ushort[,,] _geologyHandles;
        private readonly byte[,,] _surfaceBits;
        private readonly IRuntimeGeologyCatalog _geology;
        private readonly int _x;
        private readonly int _y;
        private readonly int _maxZ;

        public int X => _x;
        public int Y => _y;

        public FortressChunk(int x, int y, int maxZ, IRuntimeGeologyCatalog geology)
        {
            _geology = geology ?? throw new ArgumentNullException(nameof(geology));
            _x = x;
            _y = y;
            _maxZ = maxZ;
            _geologyHandles = new ushort[32, 32, maxZ];
            _surfaceBits = new byte[32, 32, maxZ];

            // Initialize all as granite wall
            var defaultHandle = _geology.GetGeologyHandle("core_terrain_wall_rock_granite");
            for (int lx = 0; lx < 32; lx++)
            {
                for (int ly = 0; ly < 32; ly++)
                {
                    for (int z = 0; z < maxZ; z++)
                    {
                        _geologyHandles[lx, ly, z] = defaultHandle;
                        _surfaceBits[lx, ly, z] = 0;
                    }
                }
            }
        }

        public void SetGeology(int x, int y, int z, string geologyId)
        {
            if (x >= 0 && x < 32 && y >= 0 && y < 32 && z >= 0 && z < _maxZ)
            {
                _geologyHandles[x, y, z] = _geology.GetGeologyHandle(geologyId);
            }
        }

        public void SetGeologyHandle(int x, int y, int z, ushort handle)
        {
            if (x >= 0 && x < 32 && y >= 0 && y < 32 && z >= 0 && z < _maxZ)
            {
                _geologyHandles[x, y, z] = handle;
            }
        }

        public ushort GetGeologyHandle(int x, int y, int z)
        {
            if (x >= 0 && x < 32 && y >= 0 && y < 32 && z >= 0 && z < _maxZ)
            {
                return _geologyHandles[x, y, z];
            }
            return _geology.GetGeologyHandle("core_terrain_wall_rock_granite");
        }

        // Back-compat helper for tests that expect GetTerrain
        public ushort GetTerrain(int x, int y, int z) => GetGeologyHandle(x, y, z);

        public string GetGeologyId(int x, int y, int z)
        {
            var handle = GetGeologyHandle(x, y, z);
            var geology = _geology.GetGeologyByHandle(handle);
            return geology?.Id ?? "core_terrain_wall_rock_granite";
        }

        public void SetSurfaceBits(int x, int y, int z, byte bits)
        {
            if (x >= 0 && x < 32 && y >= 0 && y < 32 && z >= 0 && z < _maxZ)
            {
                _surfaceBits[x, y, z] = bits;
            }
        }

        public byte GetSurfaceBits(int x, int y, int z)
        {
            if (x >= 0 && x < 32 && y >= 0 && y < 32 && z >= 0 && z < _maxZ)
            {
                return _surfaceBits[x, y, z];
            }
            return 0;
        }

    }
}
