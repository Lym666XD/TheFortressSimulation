using System;
using HumanFortress.Core.World;
using SadRogue.Primitives;

namespace HumanFortress.WorldGen.Implementation
{
    /// <summary>
    /// Minimal fortress generator (rolled back):
    /// - Heightmap from world tile elevation + simple noise
    /// - Surface by biome (grass/sand/snow/mud/granite_floor)
    /// - Below surface: granite wall
    /// - Above surface: air
    /// </summary>
    internal sealed partial class FortressGenerator
    {
        private readonly int _fortressSize;
        private readonly WorldTile _homeTile;
        private readonly Point _worldLocation;
        private readonly uint _seed;
        private readonly FortressGenerationContent _content;

        public FortressGenerator(
            int fortressSize,
            WorldTile homeTile,
            Point worldLocation,
            uint seed,
            FortressGenerationContent content)
        {
            _fortressSize = fortressSize;
            _homeTile = homeTile;
            _worldLocation = worldLocation;
            _seed = seed;
            _content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public FortressMap Generate()
        {
            var map = new FortressMap(_fortressSize, 50, _content.Geology);

            int tilesPerChunk = 32;
            int totalTiles = _fortressSize * tilesPerChunk;
            var mapgen = _content.MapgenTuning;
            int baseZ = 25;
            int skyAbove = 15;
            bool hillsEnabled = true;
            int rMin = 6, rMax = 12, maxDelta = 2; double density = 0.0015;
            string surfaceDefault = "core_terrain_floor_rock_limestone";
            string surfaceId = surfaceDefault;
            if (mapgen != null)
            {
                var s = Object(mapgen, "surface");
                if (s != null)
                {
                    baseZ = Value(s, "base_z", baseZ);
                    skyAbove = Value(s, "sky_above", skyAbove);
                }

                var h = Object(mapgen, "hills");
                if (h != null)
                {
                    hillsEnabled = Value(h, "enabled", hillsEnabled);
                    rMin = Value(h, "radius_min", rMin);
                    rMax = Value(h, "radius_max", rMax);
                    density = Value(h, "density", density);
                    maxDelta = Value(h, "max_delta_z", maxDelta);
                }

                var floors = Object(mapgen, "biome_surface_floor");
                if (floors != null)
                {
                    surfaceDefault = Value(floors, "Default", surfaceDefault);
                    var b = (BiomeType)_homeTile.BiomeId;
                    surfaceId = Value(floors, b.ToString(), surfaceDefault);
                }
            }

            int worldSize = _fortressSize * tilesPerChunk;
            int[,] surfZ = new int[worldSize, worldSize];

            var centers = new System.Collections.Generic.List<(int x, int y, int r)>();
            if (hillsEnabled)
            {
                var rng = new HumanFortress.Core.Random.DeterministicRng(_seed ^ 0xC0FFFEEu);
                int target = Math.Max(1, (int)(worldSize * worldSize * density));
                for (int i = 0; i < target; i++)
                {
                    int cx = (int)(rng.NextFloat() * worldSize);
                    int cy = (int)(rng.NextFloat() * worldSize);
                    int rr = rMin + (int)(rng.NextFloat() * Math.Max(1, rMax - rMin));
                    centers.Add((cx, cy, rr));
                }
            }

            for (int gx = 0; gx < worldSize; gx++)
            {
                for (int gy = 0; gy < worldSize; gy++)
                {
                    int delta = 0;
                    if (hillsEnabled)
                    {
                        double h = 0.0;
                        foreach (var c in centers)
                        {
                            int dx = gx - c.x; int dy = gy - c.y;
                            double dist2 = dx * (double)dx + dy * (double)dy;
                            double r2 = c.r * (double)c.r;
                            if (dist2 <= r2) h += 1.0 - (dist2 / r2);
                        }

                        delta = (int)Math.Clamp(Math.Round(h), 0, maxDelta);
                    }

                    surfZ[gx, gy] = Math.Clamp(baseZ + delta, 1, map.MaxZ - 1);
                }
            }

            var strataStack = GetBiomeStrata((BiomeType)_homeTile.BiomeId);

            for (int cx = 0; cx < _fortressSize; cx++)
            {
                for (int cy = 0; cy < _fortressSize; cy++)
                {
                    var chunk = map.GetChunk(cx, cy);
                    for (int lx = 0; lx < tilesPerChunk; lx++)
                    {
                        for (int ly = 0; ly < tilesPerChunk; ly++)
                        {
                            int gx = cx * tilesPerChunk + lx;
                            int gy = cy * tilesPerChunk + ly;
                            int sZ = surfZ[gx, gy];

                            if (sZ > 0)
                            {
                                ApplyStrataColumn(strataStack, chunk, lx, ly, sZ);
                            }

                            chunk.SetGeology(lx, ly, sZ, surfaceId);
                            byte surfBits = BuildSurfaceBitsForBiome((BiomeType)_homeTile.BiomeId);
                            surfBits |= 1;
                            chunk.SetSurfaceBits(lx, ly, sZ, surfBits);

                            for (int z = sZ + 1; z < map.MaxZ; z++)
                                chunk.SetGeology(lx, ly, z, "core_terrain_air");
                        }
                    }
                }
            }

            CarveCavernConnected(map, surfZ, mapgen);
            PlaceOres(map, surfZ, _content.OreTuning);

            return map;
        }
    }
}
