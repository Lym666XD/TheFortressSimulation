using HumanFortress.Core.World;

namespace HumanFortress.WorldGen.Stages
{
    public class BiomeStage : IWorldGenStage
    {
        public string Name => "Biomes";
        
        public void Execute(WorldGenContext context)
        {
            for (int x = 0; x < context.Width; x++)
            {
                for (int y = 0; y < context.Height; y++)
                {
                    var tile = context.Tiles[x, y];
                    BiomeType biome = DetermineBiome(tile.Elevation, tile.Temperature, tile.Rainfall);
                    context.Tiles[x, y].BiomeId = (ushort)biome;
                }
            }
        }
        
        private BiomeType DetermineBiome(float elevation, float temperature, float rainfall)
        {
            if (elevation < 0.2f)
                return BiomeType.Ocean;
            
            if (elevation < 0.25f)
                return BiomeType.Lake;
            
            if (elevation > 0.8f)
                return BiomeType.Mountain;
            
            if (elevation > 0.65f)
                return BiomeType.Hills;
            
            if (temperature < 0.15f)
            {
                if (rainfall < 0.2f)
                    return BiomeType.Tundra;
                else
                    return BiomeType.Glacier;
            }
            
            if (temperature < 0.35f)
            {
                if (rainfall < 0.3f)
                    return BiomeType.TemperateGrassland;
                else if (rainfall < 0.6f)
                    return BiomeType.Taiga;
                else
                    return BiomeType.TemperateForest;
            }
            
            if (temperature < 0.65f)
            {
                if (rainfall < 0.2f)
                    return BiomeType.Desert;
                else if (rainfall < 0.5f)
                    return BiomeType.Savanna;
                else
                    return BiomeType.TemperateForest;
            }
            
            if (rainfall < 0.2f)
                return BiomeType.Desert;
            else if (rainfall < 0.5f)
                return BiomeType.Savanna;
            else if (rainfall > 0.8f)
                return BiomeType.Swamp;
            else
                return BiomeType.TropicalForest;
        }
    }
}