using System;
using HumanFortress.Core.World;
using HumanFortress.Core.Content;
using Newtonsoft.Json.Linq;
using SadRogue.Primitives;

namespace HumanFortress.WorldGen
{
    using System.Collections.Generic;
    using System.Linq;
    /// <summary>
    /// Minimal fortress generator (rolled back):
    /// - Heightmap from world tile elevation + simple noise
    /// - Surface by biome (grass/sand/snow/mud/granite_floor)
    /// - Below surface: granite wall
    /// - Above surface: air
    /// </summary>
    public class FortressGenerator
    {
        private readonly int _fortressSize;
        private readonly WorldTile _homeTile;
        private readonly Point _worldLocation;
        private readonly uint _seed;

        public FortressGenerator(int fortressSize, WorldTile homeTile, Point worldLocation, uint seed)
        {
            _fortressSize = fortressSize;
            _homeTile = homeTile;
            _worldLocation = worldLocation;
            _seed = seed;
        }

        public FortressMap Generate()
        {
            var map = new FortressMap(_fortressSize, 50);

            int tilesPerChunk = 32;
            int totalTiles = _fortressSize * tilesPerChunk;
            // Tuning (data-driven)
            var mapgen = ContentRegistry.Instance.GetTuning<JObject>("tuning.mapgen", "$");
            int baseZ = 25;
            int skyAbove = 15;
            bool hillsEnabled = true;
            int rMin = 6, rMax = 12, maxDelta = 2; double density = 0.0015;
            string surfaceDefault = "core_terrain_floor_rock_limestone";
            string surfaceId = surfaceDefault;
            if (mapgen != null)
            {
                var s = mapgen["surface"] as JObject;
                if (s != null)
                {
                    baseZ = s["base_z"]?.Value<int?>() ?? baseZ;
                    skyAbove = s["sky_above"]?.Value<int?>() ?? skyAbove;
                }
                var h = mapgen["hills"] as JObject;
                if (h != null)
                {
                    hillsEnabled = h["enabled"]?.Value<bool?>() ?? hillsEnabled;
                    rMin = h["radius_min"]?.Value<int?>() ?? rMin;
                    rMax = h["radius_max"]?.Value<int?>() ?? rMax;
                    density = h["density"]?.Value<double?>() ?? density;
                    maxDelta = h["max_delta_z"]?.Value<int?>() ?? maxDelta;
                }
                var floors = mapgen["biome_surface_floor"] as JObject;
                if (floors != null)
                {
                    surfaceDefault = floors["Default"]?.Value<string>() ?? surfaceDefault;
                    var b = (BiomeType)_homeTile.BiomeId;
                    surfaceId = floors[b.ToString()]?.Value<string>() ?? surfaceDefault;
                }
            }

            int worldSize = _fortressSize * tilesPerChunk;
            int[,] surfZ = new int[worldSize, worldSize];

            // Hills: place centers deterministically
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

            // Precompute strata stack for this biome (applies fortress-wide)
            var strataStack = GetBiomeStrata((BiomeType)_homeTile.BiomeId);

            // Fill by columns
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

                            // Below surface: strata walls
                            if (sZ > 0)
                            {
                                ApplyStrataColumn(strataStack, chunk, lx, ly, sZ);
                            }

                            // Surface floor (use biome floor geology id), and set surface bits (dirt + grass/snow by biome)
                            chunk.SetGeology(lx, ly, sZ, surfaceId);
                            byte surfBits = BuildSurfaceBitsForBiome((BiomeType)_homeTile.BiomeId);
                            // Always mark surface as mud/dirt base
                            surfBits |= 1; // Mud bit
                            chunk.SetSurfaceBits(lx, ly, sZ, surfBits);

                            // Above surface: fill ALL remaining layers with air (up to MaxZ)
                            for (int z = sZ + 1; z < map.MaxZ; z++)
                                chunk.SetGeology(lx, ly, z, "core_terrain_air");
                        }
                    }
                }
            }

            // Carve single connected cavern band with floor + moss overlay
            CarveCavernConnected(map, surfZ, mapgen);

            // Place ore deposits after caverns carved
            PlaceOres(map, surfZ, ContentRegistry.Instance.GetTuning<JObject>("tuning.ore", "$"));

            return map;
        }

        // Simple hash-based noise used previously
        private static float SimplexNoise(float x, float y, uint seed)
        {
            int n = (int)(x * 1619 + y * 31337 + seed * 6971);
            n = (n << 13) ^ n;
            return (1.0f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f);
        }

        private void CarveCavernConnected(FortressMap map, int[,] surfaceZ, JObject? mapgen)
        {
            int tilesPerChunk = 32;
            int worldSize = _fortressSize * tilesPerChunk;
            var bands = mapgen? ["bands"] as JArray;
            if (bands == null || bands.Count == 0) return;

            // Pick a single cavern band (use the one with highest caves.density) and choose its mid Z as cavern floor level
            int bestZMin = 0, bestZMax = 0; double bestDensity = -1;
            foreach (var band in bands)
            {
                double d = band["caves"]? ["density"]?.Value<double?>() ?? 0.0;
                if (d > bestDensity)
                {
                    bestDensity = d;
                    bestZMin = band["z_min"]?.Value<int?>() ?? 0;
                    bestZMax = band["z_max"]?.Value<int?>() ?? 0;
                }
            }
            if (bestDensity <= 0) return;
            int zC = Math.Clamp((bestZMin + bestZMax) / 2, 1, map.MaxZ - 2);

            // Cavern tuning (data-driven)
            var cavTune = ContentRegistry.Instance.GetTuning<JObject>("tuning.cavern", "$");
            int pathThickness = cavTune?["path"]?["thickness"]?.Value<int?>() ?? 4;
            double stepsFactor = cavTune?["path"]?["steps_factor"]?.Value<double?>() ?? 6.0;
            double biasEast = cavTune?["path"]?["direction_bias_east"]?.Value<double?>() ?? 0.7;
            int tunnelWidth = cavTune?["path"]?["tunnel_width"]?.Value<int?>() ?? 3;
            int roomRMin = cavTune?["rooms"]?["radius_min"]?.Value<int?>() ?? 4;
            int roomRMax = cavTune?["rooms"]?["radius_max"]?.Value<int?>() ?? 7;
            double roomIntervalFactor = cavTune?["rooms"]?["interval_factor"]?.Value<double?>() ?? 0.15;
            int shaftsCount = cavTune?["shafts"]?["count"]?.Value<int?>() ?? 3;
            bool mossOnFloor = cavTune?["moss_on_floor"]?.Value<bool?>() ?? true;

            // Create a connected walkway/rooms from a map edge using deterministic drunkard walk
            var rng = new HumanFortress.Core.Random.DeterministicRng(_seed ^ 0xC0FEBABEu);
            var cav = new bool[worldSize, worldSize];
            int sx = 2; int sy = (int)(rng.NextFloat() * worldSize); // start near west edge
            int steps = Math.Max(worldSize * 2, (int)Math.Round(worldSize * stepsFactor));
            int x = sx, y = sy;
            for (int i = 0; i < steps; i++)
            {
                // Only carve if below surface at this column
                if (zC < surfaceZ[x, y])
                {
                    // stamp path thickness disk
                    int r = Math.Max(1, pathThickness);
                    int r2 = r * r;
                    for (int dx = -r; dx <= r; dx++)
                        for (int dy = -r; dy <= r; dy++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= worldSize || yy >= worldSize) continue;
                            if (dx * dx + dy * dy > r2) continue;
                            if (zC < surfaceZ[xx, yy]) cav[xx, yy] = true;
                        }
                }
                // Occasionally place a room
                int roomInterval = Math.Max(8, (int)Math.Round(worldSize * roomIntervalFactor));
                if (roomInterval > 0 && (i % roomInterval) == 0)
                {
                    int r = roomRMin + (int)(rng.NextFloat() * Math.Max(1, roomRMax - roomRMin));
                    for (int dx = -r; dx <= r; dx++)
                        for (int dy = -r; dy <= r; dy++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= worldSize || yy >= worldSize) continue;
                            if (dx * dx + dy * dy <= r * r && zC < surfaceZ[xx, yy]) cav[xx, yy] = true;
                        }
                }
                // Step direction biased eastward
                int dir = (int)(rng.NextFloat() * 4); // 0N 1E 2S 3W
                if (rng.NextFloat() < biasEast) dir = 1;  // bias east
                switch (dir)
                {
                    case 0: if (y > 1) y--; break;
                    case 1: if (x < worldSize - 2) x++; break;
                    case 2: if (y < worldSize - 2) y++; break;
                    case 3: if (x > 1) x--; break;
                }
            }

            // Commit cavern: set floor at zC with optional moss; open air at zC+1
            for (int gx = 0; gx < worldSize; gx++)
            {
                for (int gy = 0; gy < worldSize; gy++)
                {
                    if (!cav[gx, gy]) continue;
                    if (zC >= surfaceZ[gx, gy]) continue;
                    int cx = gx / tilesPerChunk; int lx = gx % tilesPerChunk;
                    int cy = gy / tilesPerChunk; int ly = gy % tilesPerChunk;

                    // Choose floor geology based on wall below if possible
                    string belowId = zC > 0 ? map.GetChunk(cx, cy).GetGeologyId(lx, ly, zC - 1) : "core_terrain_wall_rock_limestone";
                    string floorId = belowId.Replace("_wall_", "_floor_");
                    if (floorId == belowId) floorId = "core_terrain_floor_rock_limestone";
                    map.GetChunk(cx, cy).SetGeology(lx, ly, zC, floorId);
                    // Moss surface (bit3)
                    if (mossOnFloor)
                    {
                        byte moss = (byte)(1 << 3);
                        map.GetChunk(cx, cy).SetSurfaceBits(lx, ly, zC, moss);
                    }
                    // Open space above
                    map.GetChunk(cx, cy).SetGeology(lx, ly, zC + 1, "core_terrain_air");
                }
            }

            // Natural entrances: 2 shafts at map edges and tunnels connecting to the cavern
            for (int i = 0; i < Math.Max(1, shaftsCount); i++)
            {
                int gx = (i == 0) ? 2 : worldSize - 3;
                int gy = (int)(rng.NextFloat() * worldSize);
                int sZ = surfaceZ[gx, gy];
                int zTop = Math.Max(1, sZ);
                int zBot = Math.Max(1, zC);
                // Vertical shaft
                for (int z = zBot; z <= zTop; z++)
                {
                    int ccx = gx / tilesPerChunk; int llx = gx % tilesPerChunk;
                    int ccy = gy / tilesPerChunk; int lly = gy % tilesPerChunk;
                    map.GetChunk(ccx, ccy).SetGeology(llx, lly, z, "core_terrain_air");
                }
                // Horizontal tunnel at zC to connect shaft to cavern; stamp tunnel width
                int tx = gx, ty = gy;
                // Simple straight tunnel towards map center, carving floor + air above until hitting cav region
                int safety = worldSize * 2;
                while (!cav[tx, ty] && safety-- > 0)
                {
                    // carve one step of tunnel
                    int ccx = tx / tilesPerChunk; int llx = tx % tilesPerChunk;
                    int ccy = ty / tilesPerChunk; int lly = ty % tilesPerChunk;
                    string belowId = zC > 0 ? map.GetChunk(ccx, ccy).GetGeologyId(llx, lly, zC - 1) : "core_terrain_wall_rock_limestone";
                    string floorId = belowId.Replace("_wall_", "_floor_");
                    if (floorId == belowId) floorId = "core_terrain_floor_rock_limestone";
                    // stamp a small disk to widen tunnel
                    int r = Math.Max(1, tunnelWidth);
                    int r2 = r * r;
                    for (int dx = -r; dx <= r; dx++)
                        for (int dy = -r; dy <= r; dy++)
                        {
                            int wx = llx + dx; int wy = lly + dy; int gxw = tx + dx; int gyw = ty + dy;
                            if (gxw < 0 || gyw < 0 || gxw >= worldSize || gyw >= worldSize) continue;
                            if (dx * dx + dy * dy > r2) continue;
                            int cccx = gxw / tilesPerChunk; int wwwx = gxw % tilesPerChunk;
                            int cccy = gyw / tilesPerChunk; int wwwy = gyw % tilesPerChunk;
                            map.GetChunk(cccx, cccy).SetGeology(wwwx, wwwy, zC, floorId);
                            if (mossOnFloor) map.GetChunk(cccx, cccy).SetSurfaceBits(wwwx, wwwy, zC, (byte)(1 << 3));
                            map.GetChunk(cccx, cccy).SetGeology(wwwx, wwwy, zC + 1, "core_terrain_air");
                        }

                    // march inward
                    if (i == 0) tx++; else tx--;
                    // small vertical wiggle towards center
                    ty += ty < worldSize / 2 ? 1 : -1;
                    ty = Math.Clamp(ty, 1, worldSize - 2);
                    tx = Math.Clamp(tx, 1, worldSize - 2);
                }
            }
        }

        private record Stratum(string WallId);

        private List<Stratum> GetBiomeStrata(BiomeType biome)
        {
            // Minimal, deterministic strata per biome using available geology ids
            // Order shallow -> deep
            return biome switch
            {
                BiomeType.Mountain => new List<Stratum>
                {
                    new("core_terrain_wall_rock_basalt"),
                    new("core_terrain_wall_rock_granite"),
                    new("core_terrain_wall_rock_marble"),
                },
                BiomeType.Hills => new List<Stratum>
                {
                    new("core_terrain_wall_rock_granite"),
                    new("core_terrain_wall_rock_limestone"),
                    new("core_terrain_wall_rock_shale"),
                },
                BiomeType.Desert => new List<Stratum>
                {
                    new("core_terrain_wall_rock_sandstone"),
                    new("core_terrain_wall_rock_limestone"),
                    new("core_terrain_wall_rock_shale"),
                },
                BiomeType.Savanna => new List<Stratum>
                {
                    new("core_terrain_wall_rock_sandstone"),
                    new("core_terrain_wall_rock_limestone"),
                    new("core_terrain_wall_rock_granite"),
                },
                BiomeType.TemperateForest or BiomeType.TemperateGrassland => new List<Stratum>
                {
                    new("core_terrain_wall_rock_limestone"),
                    new("core_terrain_wall_rock_shale"),
                    new("core_terrain_wall_rock_granite"),
                },
                BiomeType.TropicalForest => new List<Stratum>
                {
                    new("core_terrain_wall_rock_basalt"),
                    new("core_terrain_wall_rock_shale"),
                    new("core_terrain_wall_rock_limestone"),
                },
                BiomeType.Taiga or BiomeType.Tundra or BiomeType.Glacier => new List<Stratum>
                {
                    new("core_terrain_wall_rock_granite"),
                    new("core_terrain_wall_rock_shale"),
                    new("core_terrain_wall_rock_basalt"),
                },
                _ => new List<Stratum>
                {
                    new("core_terrain_wall_rock_limestone"),
                    new("core_terrain_wall_rock_shale"),
                    new("core_terrain_wall_rock_granite"),
                }
            };
        }

        private static byte BuildSurfaceBitsForBiome(BiomeType biome)
        {
            // bit0 Mud, bit1 Grass, bit2 Snow. We'll set Mud separately and return only ornamental overlay.
            return biome switch
            {
                BiomeType.Glacier or BiomeType.Tundra => (byte)(1 << 2), // Snow
                BiomeType.Desert => (byte)0, // bare
                BiomeType.Swamp => (byte)0, // mostly mud
                _ => (byte)(1 << 1), // Grass for most temperate/tropical biomes
            };
        }

        private void ApplyStrataColumn(List<Stratum> strata, FortressChunk chunk, int lx, int ly, int surfaceZ)
        {
            // Divide [0 .. surfaceZ-1] into equal bands according to strata count
            int layers = Math.Max(1, strata.Count);
            // Ensure at least 1 tile per band
            int baseThickness = Math.Max(1, (surfaceZ) / layers);
            int remainder = Math.Max(0, (surfaceZ) - baseThickness * layers);

            int zCursor = 0;
            for (int i = 0; i < layers; i++)
            {
                int thickness = baseThickness + (i < remainder ? 1 : 0);
                int zEnd = Math.Min(surfaceZ, zCursor + thickness);
                for (int z = zCursor; z < zEnd; z++)
                {
                    chunk.SetGeology(lx, ly, z, strata[i].WallId);
                }
                zCursor = zEnd;
                if (zCursor >= surfaceZ) break;
            }
            // Any leftover (due to integer division) fill with deepest stratum
            for (int z = zCursor; z < surfaceZ; z++)
                chunk.SetGeology(lx, ly, z, strata[^1].WallId);
        }

        private void PlaceOres(FortressMap map, int[,] surfaceZ, JObject? oreConfig)
        {
            if (oreConfig == null) return;

            int tilesPerChunk = 32;
            int worldSize = _fortressSize * tilesPerChunk;
            int maxZ = map.MaxZ;

            float avgSurface = 0f;
            for (int x = 0; x < worldSize; x++)
                for (int y = 0; y < worldSize; y++)
                    avgSurface += surfaceZ[x, y];
            avgSurface /= Math.Max(1, worldSize * worldSize);

            int tilesPerDeposit = oreConfig["global"]?["tiles_per_deposit"]?.Value<int?>() ?? 8192;
            double densityK = oreConfig["global"]?["density_k"]?.Value<double?>() ?? 1.0;
            double abundanceMult = oreConfig["global"]?["abundance_mult"]?.Value<double?>() ?? 1.0;
            double globalSizeMult = oreConfig["global"]?["size_mult"]?.Value<double?>() ?? 1.0;

            var ores = oreConfig["ores"] as JArray;
            if (ores == null) return;

            var rng = new HumanFortress.Core.Random.DeterministicRng(_seed ^ 0x03BAB1Eu);

            foreach (var ore in ores)
            {
                string oreId = ore["id"]?.Value<string>() ?? string.Empty;
                if (string.IsNullOrEmpty(oreId)) continue;

                var allowed = ore["allowed_host_tags"] as JArray;
                var allowedTags = allowed != null
                    ? allowed.Values<string>().Where(s => s != null).Select(s => s!).ToHashSet()
                    : new HashSet<string>();
                string rarity = ore["rarity"]?.Value<string>()?.ToLowerInvariant() ?? "common";
                double rarityW = rarity switch { "common" => 1.0, "uncommon" => 0.5, "rare" => 0.25, _ => 1.0 };

                double volume = worldSize * worldSize * Math.Max(1.0, avgSurface);
                int deposits = Math.Max(1, (int)(volume / tilesPerDeposit * densityK * rarityW * abundanceMult));

                string form = ore["form"]?.Value<string>() ?? "vein";
                for (int d = 0; d < deposits; d++)
                {
                    // Pick a random valid start
                    for (int attempts = 0; attempts < 200; attempts++)
                    {
                        int gx = (int)(rng.NextFloat() * worldSize);
                        int gy = (int)(rng.NextFloat() * worldSize);
                        int sZ = surfaceZ[gx, gy];
                        if (sZ <= 2) continue; // too shallow
                        int gz = 1 + (int)(rng.NextFloat() * Math.Max(1, sZ - 2));

                        if (!IsHostRock(map, gx, gy, gz, allowedTags))
                            continue;

                        if (form == "blob")
                        {
                            var blob = ore["blob"] as JObject;
                            var size = blob?["size"] as JArray; // total tiles target (approx)
                            var rad = blob?["radius"] as JArray;
                            int rMin = rad?.ElementAtOrDefault(0)?.Value<int?>() ?? 2;
                            int rMax = rad?.ElementAtOrDefault(1)?.Value<int?>() ?? 4;
                            int radius = rMin + (int)(rng.NextFloat() * Math.Max(0, rMax - rMin));
                            double oreRadiusMult = ore["radius_mult"]?.Value<double?>() ?? 1.0;
                            radius = (int)Math.Max(1, Math.Round(radius * globalSizeMult * oreRadiusMult));
                            StampBlob(map, surfaceZ, gx, gy, gz, radius, oreId, allowedTags);
                        }
                        else // vein
                        {
                            var vein = ore["vein"] as JObject;
                            var size = vein?["size"] as JArray;
                            var thick = vein?["thickness"] as JArray;
                            int lenMin = size?.ElementAtOrDefault(0)?.Value<int?>() ?? 40;
                            int lenMax = size?.ElementAtOrDefault(1)?.Value<int?>() ?? 100;
                            int len = lenMin + (int)(rng.NextFloat() * Math.Max(1, lenMax - lenMin));
                            int tMin = thick?.ElementAtOrDefault(0)?.Value<int?>() ?? 1;
                            int tMax = thick?.ElementAtOrDefault(1)?.Value<int?>() ?? 2;
                            int thickness = tMin + (int)(rng.NextFloat() * Math.Max(0, tMax - tMin));
                            double oreSizeMult = ore["size_mult"]?.Value<double?>() ?? 1.0;
                            double oreThickMult = ore["thickness_mult"]?.Value<double?>() ?? 1.0;
                            len = (int)Math.Max(5, Math.Round(len * globalSizeMult * oreSizeMult));
                            thickness = (int)Math.Max(1, Math.Round(thickness * oreThickMult));
                            double orientBias = vein?["orientation_bias"]?.Value<double?>() ?? 0.5;
                            double branchChance = vein?["branch_chance"]?.Value<double?>() ?? 0.05;
                            GrowVein(map, surfaceZ, gx, gy, gz, len, thickness, oreId, allowedTags, rng, orientBias, branchChance);
                        }
                        break; // next deposit
                    }
                }
            }
        }

        private bool IsHostRock(FortressMap map, int gx, int gy, int gz, HashSet<string> allowedTags)
        {
            int cx = gx / 32; int lx = gx % 32; int cy = gy / 32; int ly = gy % 32;
            if (cx < 0 || cy < 0 || cx >= _fortressSize || cy >= _fortressSize) return false;
            var chunk = map.GetChunk(cx, cy);
            var handle = chunk.GetGeologyHandle(lx, ly, gz);
            var geo = ContentRegistry.Instance.GetGeologyByHandle(handle);
            if (geo == null) return false;
            // Must be a SolidWall and have at least one allowed tag
            if (!Enum.TryParse<TerrainKind>(geo.TerrainBits.Kind, out var k) || k != TerrainKind.SolidWall)
                return false;
            if (allowedTags == null || allowedTags.Count == 0) return true; // if none specified, allow any wall
            return geo.Tags.Any(t => allowedTags.Contains(t));
        }

        private void StampBlob(FortressMap map, int[,] surfaceZ, int sx, int sy, int sz, int radius, string oreId, HashSet<string> allowedTags)
        {
            int worldSize = _fortressSize * 32;
            int r2 = radius * radius;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = sx + dx; int y = sy + dy; int z = sz;
                    if (x < 0 || y < 0 || x >= worldSize || y >= worldSize) continue;
                    if (dx * dx + dy * dy > r2) continue;
                    // Optionally extend to adjacent z
                    for (int dz = 0; dz <= 1; dz++)
                    {
                        int zz = z + (dz == 0 ? 0 : (dx + dy) % 2 == 0 ? 1 : -1);
                        if (zz <= 0) continue;
                        if (zz >= surfaceZ[x, y]) continue;
                        if (!IsHostRock(map, x, y, zz, allowedTags)) continue;
                        int cx = x / 32; int lx = x % 32; int cy = y / 32; int ly = y % 32;
                        map.GetChunk(cx, cy).SetGeology(lx, ly, zz, oreId);
                    }
                }
            }
        }

        private void GrowVein(FortressMap map, int[,] surfaceZ, int sx, int sy, int sz, int length, int thickness, string oreId, HashSet<string> allowedTags, HumanFortress.Core.Random.DeterministicRng rng, double orientBias, double branchChance)
        {
            int worldSize = _fortressSize * 32;
            // Initial direction
            int dir = (int)(rng.NextFloat() * 8); // 0..7 around
            int x = sx, y = sy, z = sz;
            for (int i = 0; i < length; i++)
            {
                // Stamp cross-section
                StampDisk(map, surfaceZ, x, y, z, thickness, oreId, allowedTags);

                // Move direction with bias to keep going mostly straight
                if (rng.NextFloat() > orientBias)
                {
                    dir = (dir + (rng.NextFloat() < 0.5f ? -1 : 1) + 8) % 8;
                }
                // Step
                var step = dir switch
                {
                    0 => (0, -1),  // N
                    1 => (1, -1),  // NE
                    2 => (1, 0),   // E
                    3 => (1, 1),   // SE
                    4 => (0, 1),   // S
                    5 => (-1, 1),  // SW
                    6 => (-1, 0),  // W
                    _ => (-1, -1), // NW
                };
                x += step.Item1; y += step.Item2;
                // Occasional gentle vertical wiggle
                if (rng.NextFloat() < 0.15f)
                    z += rng.NextFloat() < 0.5f ? -1 : 1;

                // Bounds & surface check
                if (x < 0 || y < 0 || x >= worldSize || y >= worldSize) break;
                if (z <= 1) z = 1;
                if (z >= surfaceZ[x, y] - 1) z = surfaceZ[x, y] - 2;

                // Branch
                if (rng.NextFloat() < branchChance)
                {
                    int bx = x, by = y, bz = z;
                    int bdir = (dir + (rng.NextFloat() < 0.5f ? -2 : 2) + 8) % 8;
                    int blen = Math.Max(5, length / 4);
                    GrowVein(map, surfaceZ, bx, by, bz, blen, Math.Max(1, thickness - 1), oreId, allowedTags, rng, orientBias, branchChance * 0.5);
                }
            }
        }

        private void StampDisk(FortressMap map, int[,] surfaceZ, int cx, int cy, int cz, int radius, string oreId, HashSet<string> allowedTags)
        {
            int worldSize = _fortressSize * 32;
            int r2 = radius * radius;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = cx + dx; int y = cy + dy; int z = cz;
                    if (x < 0 || y < 0 || x >= worldSize || y >= worldSize) continue;
                    if (dx * dx + dy * dy > r2) continue;
                    if (z <= 0 || z >= surfaceZ[x, y]) continue;
                    if (!IsHostRock(map, x, y, z, allowedTags)) continue;
                    int chx = x / 32; int lx = x % 32; int chy = y / 32; int ly = y % 32;
                    map.GetChunk(chx, chy).SetGeology(lx, ly, z, oreId);
                }
            }
        }
    }
}
