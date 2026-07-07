using System;
using System.Collections.Generic;
using HumanFortress.Core.World;

namespace HumanFortress.WorldGen.Implementation
{
    internal sealed partial class FortressGenerator
    {
        private record Stratum(string WallId);

        private List<Stratum> GetBiomeStrata(BiomeType biome)
        {
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
            return biome switch
            {
                BiomeType.Glacier or BiomeType.Tundra => (byte)(1 << 2),
                BiomeType.Desert => (byte)0,
                BiomeType.Swamp => (byte)0,
                _ => (byte)(1 << 1),
            };
        }

        private void ApplyStrataColumn(List<Stratum> strata, FortressChunk chunk, int lx, int ly, int surfaceZ)
        {
            int layers = Math.Max(1, strata.Count);
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

            for (int z = zCursor; z < surfaceZ; z++)
                chunk.SetGeology(lx, ly, z, strata[^1].WallId);
        }
    }
}
