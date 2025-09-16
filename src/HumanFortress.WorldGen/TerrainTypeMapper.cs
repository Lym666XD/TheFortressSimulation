using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Core.Content;
using HumanFortress.Core.World;

namespace HumanFortress.WorldGen
{
    /// <summary>
    /// Maps between old hardcoded TerrainType enum and new data-driven geology system.
    /// </summary>
    public static class TerrainTypeMapper
    {
        private static readonly Dictionary<TerrainType, string> _terrainToGeologyId = new()
        {
            { TerrainType.Air, "terrain_air" },
            { TerrainType.Stone, "terrain_granite_wall" },
            { TerrainType.Grass, "terrain_grass" },
            { TerrainType.Sand, "terrain_sand" },
            { TerrainType.Snow, "terrain_snow" },
            { TerrainType.Mud, "terrain_mud" },
            { TerrainType.Rock, "terrain_granite_wall" },
            { TerrainType.CavernFloor, "terrain_cavern_floor" },
            { TerrainType.OreVein, "terrain_ore_iron" },
            // Geological strata
            { TerrainType.Granite, "terrain_granite_wall" },
            { TerrainType.Marble, "terrain_marble_wall" },
            { TerrainType.Basalt, "terrain_basalt_wall" },
            { TerrainType.Sandstone, "terrain_sandstone_wall" },
            { TerrainType.Limestone, "terrain_limestone_wall" },
            { TerrainType.Shale, "terrain_shale_wall" }
        };

        private static readonly Dictionary<BiomeType, TerrainType[]> _biomeStrata = new()
        {
            { BiomeType.Mountain, new[] { TerrainType.Granite, TerrainType.Marble, TerrainType.Basalt } },
            { BiomeType.Desert, new[] { TerrainType.Sandstone, TerrainType.Limestone } },
            { BiomeType.TemperateForest, new[] { TerrainType.Limestone, TerrainType.Shale } },
            { BiomeType.TemperateGrassland, new[] { TerrainType.Limestone, TerrainType.Shale } },
            { BiomeType.Taiga, new[] { TerrainType.Granite, TerrainType.Shale } },
            { BiomeType.Tundra, new[] { TerrainType.Granite, TerrainType.Basalt } },
            { BiomeType.Savanna, new[] { TerrainType.Sandstone, TerrainType.Limestone } },
            { BiomeType.TropicalForest, new[] { TerrainType.Basalt, TerrainType.Limestone } },
            { BiomeType.Swamp, new[] { TerrainType.Limestone, TerrainType.Shale } },
            { BiomeType.Hills, new[] { TerrainType.Granite, TerrainType.Limestone } }
        };

        /// <summary>
        /// Get the geology ID for a terrain type.
        /// </summary>
        public static string GetGeologyId(TerrainType terrain)
        {
            return _terrainToGeologyId.TryGetValue(terrain, out var id) ? id : "terrain_air";
        }

        /// <summary>
        /// Get the terrain type from geology ID (for backwards compatibility).
        /// </summary>
        public static TerrainType GetTerrainType(string geologyId)
        {
            var entry = _terrainToGeologyId.FirstOrDefault(kvp => kvp.Value == geologyId);
            return entry.Value != null ? entry.Key : TerrainType.Air;
        }

        /// <summary>
        /// Get geological strata for a biome.
        /// </summary>
        public static TerrainType[] GetBiomeStrata(BiomeType biome)
        {
            return _biomeStrata.TryGetValue(biome, out var strata)
                ? strata
                : new[] { TerrainType.Stone, TerrainType.Limestone };
        }

        /// <summary>
        /// Get the surface terrain for a biome.
        /// </summary>
        public static TerrainType GetBiomeSurface(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Desert => TerrainType.Sand,
                BiomeType.Tundra => TerrainType.Snow,
                BiomeType.Mountain => TerrainType.Rock,
                BiomeType.Swamp => TerrainType.Mud,
                _ => TerrainType.Grass
            };
        }

        /// <summary>
        /// Convert terrain type to geology data handle for efficient storage.
        /// </summary>
        public static ushort GetGeologyHandle(TerrainType terrain)
        {
            var geologyId = GetGeologyId(terrain);
            return ContentRegistry.Instance.GetGeologyHandle(geologyId);
        }

        /// <summary>
        /// Get display information from geology data.
        /// </summary>
        public static (int glyph, SadRogue.Primitives.Color foreground, SadRogue.Primitives.Color background)
            GetTerrainDisplay(TerrainType terrain)
        {
            var geologyId = GetGeologyId(terrain);
            var geology = ContentRegistry.Instance.GetGeology(geologyId);

            if (geology != null)
            {
                var fg = new SadRogue.Primitives.Color(
                    geology.Display.Foreground.R,
                    geology.Display.Foreground.G,
                    geology.Display.Foreground.B);
                var bg = new SadRogue.Primitives.Color(
                    geology.Display.Background.R,
                    geology.Display.Background.G,
                    geology.Display.Background.B);
                return (geology.Display.Glyph, fg, bg);
            }

            // Fallback
            return ('?', SadRogue.Primitives.Color.Magenta, SadRogue.Primitives.Color.Black);
        }

        /// <summary>
        /// Get navigation cost from geology data.
        /// </summary>
        public static int GetNavCost(TerrainType terrain)
        {
            var geologyId = GetGeologyId(terrain);
            var geology = ContentRegistry.Instance.GetGeology(geologyId);
            return geology?.Properties.NavCostBase ?? 10;
        }

        /// <summary>
        /// Check if terrain is mineable.
        /// </summary>
        public static bool IsMineable(TerrainType terrain)
        {
            var geologyId = GetGeologyId(terrain);
            var geology = ContentRegistry.Instance.GetGeology(geologyId);
            return geology?.Properties.Mineable ?? false;
        }

        /// <summary>
        /// Get ore spawn chance for a terrain type.
        /// </summary>
        public static float GetOreChance(TerrainType terrain)
        {
            var geologyId = GetGeologyId(terrain);
            var geology = ContentRegistry.Instance.GetGeology(geologyId);
            return geology?.Properties.OreChance ?? 0f;
        }
    }
}