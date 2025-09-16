using System;
using System.Collections.Generic;
using HumanFortress.Core.Content;
using HumanFortress.Core.Random;
using HumanFortress.Core.World;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.WorldGen
{
    /// <summary>
    /// Generates fortress map from world tile context per MAPGEN_PIPELINE.md section 4.
    /// </summary>
    public class FortressGenerator
    {
        private readonly int _fortressSize; // S in S×S chunks
        private readonly WorldTile _homeTile;
        private readonly Point _worldLocation;
        private readonly uint _seed;
        private readonly List<StrataLayer> _strataLayers;
        
        public FortressGenerator(int fortressSize, WorldTile homeTile, Point worldLocation, uint seed)
        {
            _fortressSize = fortressSize;
            _homeTile = homeTile;
            _worldLocation = worldLocation;
            _seed = seed;
            _strataLayers = new List<StrataLayer>();
        }
        
        /// <summary>
        /// Generate the fortress map.
        /// </summary>
        public FortressMap Generate()
        {
            var rng = new DeterministicRng(_seed);
            var map = new FortressMap(_fortressSize, 50); // 50 Z-levels

            // Phase 0: Generate geological strata
            GenerateGeology(rng);

            // Phase 1: Surface synthesis
            GenerateSurface(map, rng);

            // Phase 2: Subsurface synthesis with proper strata
            GenerateSubsurface(map, rng);

            // Phase 3: Single cavern system per spec
            GenerateCavernSystem(map, rng);

            // Phase 4: Place ore veins in strata
            PlaceOreVeins(map, rng);

            // Phase 5: Place initial resources
            PlaceResources(map, rng);

            // Phase 6: Playability checks
            VerifyPlayability(map);

            return map;
        }
        
        private void GenerateSurface(FortressMap map, DeterministicRng rng)
        {
            try
            {
                int tilesPerChunk = 32;
                int totalTiles = _fortressSize * tilesPerChunk;
                System.Console.WriteLine($"[GenerateSurface] Generating {totalTiles}x{totalTiles} tiles");

                // Generate heightmap based on world tile elevation
                float baseElevation = _homeTile.Elevation;
                int surfaceZ = (int)(25 + baseElevation * 10); // Surface level between Z=25-35
                System.Console.WriteLine($"[GenerateSurface] Base elevation: {baseElevation}, Surface Z: {surfaceZ}");

                for (int cx = 0; cx < _fortressSize; cx++)
                {
                    for (int cy = 0; cy < _fortressSize; cy++)
                    {
                        var chunk = map.GetChunk(cx, cy);

                        for (int lx = 0; lx < tilesPerChunk; lx++)
                        {
                            for (int ly = 0; ly < tilesPerChunk; ly++)
                            {
                                int globalX = cx * tilesPerChunk + lx;
                                int globalY = cy * tilesPerChunk + ly;

                                // Add some local variation to surface height
                                float noise = SimplexNoise(globalX * 0.1f, globalY * 0.1f, _seed);
                                int localZ = surfaceZ + (int)(noise * 3);

                                // Set terrain based on biome
                                SetTerrainForBiome(chunk, lx, ly, localZ, (BiomeType)_homeTile.BiomeId);
                            }
                        }
                    }
                }

                System.Console.WriteLine($"[GenerateSurface] Surface generation complete for biome: {(BiomeType)_homeTile.BiomeId}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[GenerateSurface] ERROR: {ex.Message}");
                throw;
            }
        }

        private void GenerateGeology(DeterministicRng rng)
        {
            // Generate geological strata layers based on biome
            int numLayers = 3 + rng.NextInt(4); // 3-6 strata layers
            int currentDepth = 0;

            for (int i = 0; i < numLayers; i++)
            {
                var layer = new StrataLayer
                {
                    StartZ = currentDepth,
                    Thickness = 5 + rng.NextInt(10),
                    MaterialType = GetStrataType((BiomeType)_homeTile.BiomeId, i, rng),
                    OreChance = 0.02f + rng.NextFloat() * 0.03f
                };
                _strataLayers.Add(layer);
                currentDepth += layer.Thickness;
                if (currentDepth >= 25) break; // Stop at surface level
            }
        }

        private TerrainType GetStrataType(BiomeType biome, int layerIndex, DeterministicRng rng)
        {
            // Use the data-driven biome strata from TerrainTypeMapper
            var strataTypes = TerrainTypeMapper.GetBiomeStrata(biome);

            // Cycle through available strata for this biome
            if (strataTypes.Length > 0)
            {
                return strataTypes[layerIndex % strataTypes.Length];
            }

            // Fallback to limestone if no strata defined
            return TerrainType.Limestone;
        }

        private void GenerateSubsurface(FortressMap map, DeterministicRng rng)
        {
            // Apply geological strata to all chunks
            for (int cx = 0; cx < _fortressSize; cx++)
            {
                for (int cy = 0; cy < _fortressSize; cy++)
                {
                    var chunk = map.GetChunk(cx, cy);
                    ApplyStrata(chunk, rng);
                }
            }
        }

        private void ApplyStrata(FortressChunk chunk, DeterministicRng rng)
        {
            // Apply geological layers to chunk
            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 32; y++)
                {
                    foreach (var layer in _strataLayers)
                    {
                        for (int z = layer.StartZ; z < layer.StartZ + layer.Thickness && z < 25; z++)
                        {
                            // Add some variation to layer boundaries
                            float noise = SimplexNoise(x * 0.2f, y * 0.2f, _seed + (uint)z);
                            int adjustedZ = z + (int)(noise * 2);
                            if (adjustedZ >= 0 && adjustedZ < 50)
                            {
                                chunk.SetTerrain(x, y, adjustedZ, layer.MaterialType);
                            }
                        }
                    }
                }
            }
        }
        
        private void GenerateCavernLayer(FortressChunk chunk, int cavernZ, DeterministicRng rng)
        {
            // Simple cavern generation using cellular automata
            int size = 32;
            bool[,] cavern = new bool[size, size];
            
            // Initial random seed
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    cavern[x, y] = rng.NextFloat() < 0.45f;
                }
            }
            
            // Apply cellular automata rules
            for (int iteration = 0; iteration < 3; iteration++)
            {
                bool[,] next = new bool[size, size];
                for (int x = 1; x < size - 1; x++)
                {
                    for (int y = 1; y < size - 1; y++)
                    {
                        int neighbors = CountNeighbors(cavern, x, y);
                        next[x, y] = neighbors >= 5 || (neighbors >= 4 && cavern[x, y]);
                    }
                }
                cavern = next;
            }
            
            // Apply cavern to chunk
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (!cavern[x, y])
                    {
                        chunk.SetTerrain(x, y, cavernZ, TerrainType.CavernFloor);
                    }
                }
            }
        }
        
        private int CountNeighbors(bool[,] grid, int x, int y)
        {
            int count = 0;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (grid[x + dx, y + dy]) count++;
                }
            }
            return count;
        }
        
        private void GenerateCavernSystem(FortressMap map, DeterministicRng rng)
        {
            // Generate single connected cavern system per MAPGEN_PIPELINE.md
            int cavernZ = 10 + rng.NextInt(8); // Main cavern layer Z=10-18

            // Use perlin noise for connected caverns
            float[,] cavernNoise = new float[_fortressSize * 32, _fortressSize * 32];
            for (int x = 0; x < _fortressSize * 32; x++)
            {
                for (int y = 0; y < _fortressSize * 32; y++)
                {
                    float noise1 = SimplexNoise(x * 0.05f, y * 0.05f, _seed);
                    float noise2 = SimplexNoise(x * 0.1f, y * 0.1f, _seed + 1000);
                    cavernNoise[x, y] = (noise1 + noise2 * 0.5f) / 1.5f;
                }
            }

            // Apply caverns to chunks
            for (int cx = 0; cx < _fortressSize; cx++)
            {
                for (int cy = 0; cy < _fortressSize; cy++)
                {
                    var chunk = map.GetChunk(cx, cy);
                    for (int lx = 0; lx < 32; lx++)
                    {
                        for (int ly = 0; ly < 32; ly++)
                        {
                            int gx = cx * 32 + lx;
                            int gy = cy * 32 + ly;

                            // Create cavern if noise exceeds threshold
                            if (cavernNoise[gx, gy] > 0.3f)
                            {
                                // Main cavern level
                                chunk.SetTerrain(lx, ly, cavernZ, TerrainType.CavernFloor);
                                // Add some height variation
                                if (cavernNoise[gx, gy] > 0.5f)
                                {
                                    chunk.SetTerrain(lx, ly, cavernZ + 1, TerrainType.CavernFloor);
                                }
                                if (cavernNoise[gx, gy] > 0.4f && cavernZ > 0)
                                {
                                    chunk.SetTerrain(lx, ly, cavernZ - 1, TerrainType.CavernFloor);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void PlaceOreVeins(FortressMap map, DeterministicRng rng)
        {
            // Place ore veins in appropriate strata layers
            for (int cx = 0; cx < _fortressSize; cx++)
            {
                for (int cy = 0; cy < _fortressSize; cy++)
                {
                    var chunk = map.GetChunk(cx, cy);
                    PlaceChunkOreVeins(chunk, rng);
                }
            }
        }

        private void PlaceChunkOreVeins(FortressChunk chunk, DeterministicRng rng)
        {
            // Place ore veins based on strata layers
            foreach (var layer in _strataLayers)
            {
                if (rng.NextFloat() < layer.OreChance)
                {
                    PlaceOreVein(chunk, layer.StartZ, layer.StartZ + layer.Thickness, rng);
                }
            }
        }

        private void PlaceOreVein(FortressChunk chunk, int minZ, int maxZ, DeterministicRng rng)
        {
            int veinCount = 1 + rng.NextInt(3);
            for (int i = 0; i < veinCount; i++)
            {
                int x = rng.NextInt(32);
                int y = rng.NextInt(32);
                int z = minZ + rng.NextInt(Math.Max(1, maxZ - minZ));
                int size = 3 + rng.NextInt(5);
                
                // Simple blob of ore
                for (int dx = -size/2; dx <= size/2; dx++)
                {
                    for (int dy = -size/2; dy <= size/2; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int tx = x + dx;
                            int ty = y + dy;
                            int tz = z + dz;
                            
                            if (tx >= 0 && tx < 32 && ty >= 0 && ty < 32 && tz >= 0 && tz < 50)
                            {
                                if (rng.NextFloat() < 0.7f)
                                {
                                    chunk.SetTerrain(tx, ty, tz, TerrainType.OreVein);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private void PlaceResources(FortressMap map, DeterministicRng rng)
        {
            // Place some initial trees, plants, etc. based on biome
            // This is simplified for now
        }
        
        private void VerifyPlayability(FortressMap map)
        {
            // Ensure map is playable - has accessible areas, resources, etc.
            bool hasAccessibleSurface = false;
            bool hasWater = false;
            bool hasOre = false;

            for (int cx = 0; cx < _fortressSize && !hasAccessibleSurface; cx++)
            {
                for (int cy = 0; cy < _fortressSize && !hasAccessibleSurface; cy++)
                {
                    var chunk = map.GetChunk(cx, cy);
                    for (int x = 0; x < 32; x++)
                    {
                        for (int y = 0; y < 32; y++)
                        {
                            for (int z = 20; z < 35; z++)
                            {
                                var terrain = chunk.GetTerrain(x, y, z);
                                if (terrain == TerrainType.Grass || terrain == TerrainType.Sand ||
                                    terrain == TerrainType.Snow || terrain == TerrainType.Mud)
                                {
                                    hasAccessibleSurface = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            System.Console.WriteLine($"[VerifyPlayability] Accessible surface: {hasAccessibleSurface}");
            System.Console.WriteLine($"[VerifyPlayability] Has water source: {hasWater}");
            System.Console.WriteLine($"[VerifyPlayability] Has ore deposits: {hasOre}");
        }

        private void SetTerrainForBiome(FortressChunk chunk, int x, int y, int surfaceZ, BiomeType biome)
        {
            // Use data-driven surface terrain from TerrainTypeMapper
            TerrainType surface = TerrainTypeMapper.GetBiomeSurface(biome);

            chunk.SetTerrain(x, y, surfaceZ, surface);

            // Fill below surface with stone (will be overwritten by strata later)
            for (int z = 0; z < surfaceZ; z++)
            {
                chunk.SetTerrain(x, y, z, TerrainType.Stone);
            }

            // Air above surface
            for (int z = surfaceZ + 1; z < 50; z++)
            {
                chunk.SetTerrain(x, y, z, TerrainType.Air);
            }
        }
        
        private float SimplexNoise(float x, float y, uint seed)
        {
            // Simple pseudo-random noise for terrain variation
            int n = (int)(x * 1619 + y * 31337 + seed * 6971);
            n = (n << 13) ^ n;
            return (1.0f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f);
        }
    }
    
    /// <summary>
    /// Terrain types for fortress generation.
    /// </summary>
    public enum TerrainType : ushort
    {
        Air = 0,
        Stone = 1,
        Grass = 2,
        Sand = 3,
        Snow = 4,
        Mud = 5,
        Rock = 6,
        CavernFloor = 7,
        OreVein = 8,
        // Geological strata types
        Granite = 9,
        Marble = 10,
        Basalt = 11,
        Sandstone = 12,
        Limestone = 13,
        Shale = 14
    }

    /// <summary>
    /// Represents a geological stratum layer.
    /// </summary>
    internal class StrataLayer
    {
        public int StartZ { get; set; }
        public int Thickness { get; set; }
        public TerrainType MaterialType { get; set; }
        public float OreChance { get; set; }
    }
}