using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TerrainKind = HumanFortress.Simulation.Tiles.TerrainKind;

namespace HumanFortress.WorldGen.Implementation
{
    internal sealed partial class FortressGenerator
    {
        private void PlaceOres(FortressMap map, int[,] surfaceZ, JsonObject? oreConfig)
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

            var global = Object(oreConfig, "global");
            int tilesPerDeposit = Value(global, "tiles_per_deposit", 8192);
            double densityK = Value(global, "density_k", 1.0);
            double abundanceMult = Value(global, "abundance_mult", 1.0);
            double globalSizeMult = Value(global, "size_mult", 1.0);

            var ores = Array(oreConfig, "ores");
            if (ores == null) return;

            var rng = new HumanFortress.Core.Random.DeterministicRng(_seed ^ 0x03BAB1Eu);

            foreach (var oreNode in ores)
            {
                var ore = oreNode as JsonObject;
                if (ore == null) continue;

                string oreId = Value(ore, "id", string.Empty);
                if (string.IsNullOrEmpty(oreId)) continue;

                var allowed = Array(ore, "allowed_host_tags");
                var allowedTags = allowed != null
                    ? StringValues(allowed).ToHashSet()
                    : new HashSet<string>();
                string rarity = Value(ore, "rarity", "common").ToLowerInvariant();
                double rarityW = rarity switch { "common" => 1.0, "uncommon" => 0.5, "rare" => 0.25, _ => 1.0 };

                double volume = worldSize * worldSize * Math.Max(1.0, avgSurface);
                int deposits = Math.Max(1, (int)(volume / tilesPerDeposit * densityK * rarityW * abundanceMult));

                string form = Value(ore, "form", "vein");
                for (int d = 0; d < deposits; d++)
                {
                    for (int attempts = 0; attempts < 200; attempts++)
                    {
                        int gx = (int)(rng.NextFloat() * worldSize);
                        int gy = (int)(rng.NextFloat() * worldSize);
                        int sZ = surfaceZ[gx, gy];
                        if (sZ <= 2) continue;
                        int gz = 1 + (int)(rng.NextFloat() * Math.Max(1, sZ - 2));

                        if (!IsHostRock(map, gx, gy, gz, allowedTags))
                            continue;

                        if (form == "blob")
                        {
                            var blob = Object(ore, "blob");
                            var rad = Array(blob, "radius");
                            int rMin = ArrayValue(rad, 0, 2);
                            int rMax = ArrayValue(rad, 1, 4);
                            int radius = rMin + (int)(rng.NextFloat() * Math.Max(0, rMax - rMin));
                            double oreRadiusMult = Value(ore, "radius_mult", 1.0);
                            radius = (int)Math.Max(1, Math.Round(radius * globalSizeMult * oreRadiusMult));
                            StampBlob(map, surfaceZ, gx, gy, gz, radius, oreId, allowedTags);
                        }
                        else
                        {
                            var vein = Object(ore, "vein");
                            var size = Array(vein, "size");
                            var thick = Array(vein, "thickness");
                            int lenMin = ArrayValue(size, 0, 40);
                            int lenMax = ArrayValue(size, 1, 100);
                            int len = lenMin + (int)(rng.NextFloat() * Math.Max(1, lenMax - lenMin));
                            int tMin = ArrayValue(thick, 0, 1);
                            int tMax = ArrayValue(thick, 1, 2);
                            int thickness = tMin + (int)(rng.NextFloat() * Math.Max(0, tMax - tMin));
                            double oreSizeMult = Value(ore, "size_mult", 1.0);
                            double oreThickMult = Value(ore, "thickness_mult", 1.0);
                            len = (int)Math.Max(5, Math.Round(len * globalSizeMult * oreSizeMult));
                            thickness = (int)Math.Max(1, Math.Round(thickness * oreThickMult));
                            double orientBias = Value(vein, "orientation_bias", 0.5);
                            double branchChance = Value(vein, "branch_chance", 0.05);
                            GrowVein(map, surfaceZ, gx, gy, gz, len, thickness, oreId, allowedTags, rng, orientBias, branchChance);
                        }

                        break;
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
            var geo = _content.Geology.GetGeologyByHandle(handle);
            if (geo == null) return false;
            if (!Enum.TryParse<TerrainKind>(geo.TerrainBits.Kind, out var k) || k != TerrainKind.SolidWall)
                return false;
            if (allowedTags == null || allowedTags.Count == 0) return true;
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

        private void GrowVein(
            FortressMap map,
            int[,] surfaceZ,
            int sx,
            int sy,
            int sz,
            int length,
            int thickness,
            string oreId,
            HashSet<string> allowedTags,
            HumanFortress.Core.Random.DeterministicRng rng,
            double orientBias,
            double branchChance)
        {
            int worldSize = _fortressSize * 32;
            int dir = (int)(rng.NextFloat() * 8);
            int x = sx, y = sy, z = sz;
            for (int i = 0; i < length; i++)
            {
                StampDisk(map, surfaceZ, x, y, z, thickness, oreId, allowedTags);

                if (rng.NextFloat() > orientBias)
                {
                    dir = (dir + (rng.NextFloat() < 0.5f ? -1 : 1) + 8) % 8;
                }

                var step = dir switch
                {
                    0 => (0, -1),
                    1 => (1, -1),
                    2 => (1, 0),
                    3 => (1, 1),
                    4 => (0, 1),
                    5 => (-1, 1),
                    6 => (-1, 0),
                    _ => (-1, -1),
                };
                x += step.Item1; y += step.Item2;
                if (rng.NextFloat() < 0.15f)
                    z += rng.NextFloat() < 0.5f ? -1 : 1;

                if (x < 0 || y < 0 || x >= worldSize || y >= worldSize) break;
                if (z <= 1) z = 1;
                if (z >= surfaceZ[x, y] - 1) z = surfaceZ[x, y] - 2;

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
