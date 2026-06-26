using System;
using HumanFortress.Core.Random;
using HumanFortress.Core.World;

namespace HumanFortress.WorldGen.Stages
{
    internal sealed class ElevationStage : IWorldGenStage
    {
        public string Name => "Elevation";
        
        public void Execute(WorldGenContext context)
        {
            var seed = context.GetStageSeed(Name);
            var rng = new DeterministicRng(seed);

            for (int x = 0; x < context.Width; x++)
            {
                for (int y = 0; y < context.Height; y++)
                {
                    float elevation = GenerateElevation(x, y, context.Width, context.Height, seed);
                    context.Tiles[x, y].Elevation = elevation;
                }
            }
        }

        private float GenerateElevation(int x, int y, int width, int height, uint seed)
        {
            float nx = (float)x / width;
            float ny = (float)y / height;

            float ridged = RidgedNoise(nx * 4, ny * 4, seed);
            float simplex = SimplexNoise(nx * 8, ny * 8, seed + 1000);
            
            float elevation = (ridged * 0.7f + simplex * 0.3f);
            
            elevation = Math.Max(0, Math.Min(1, elevation));
            
            return elevation;
        }
        
        private float RidgedNoise(float x, float y, uint seed)
        {
            float value = 0;
            float amplitude = 1;
            float frequency = 1;
            float maxValue = 0;
            
            for (int i = 0; i < 4; i++)
            {
                float n = 1 - Math.Abs(PseudoRandom(x * frequency + seed, y * frequency + seed));
                n = n * n;
                value += n * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2;
            }
            
            return value / maxValue;
        }
        
        private float SimplexNoise(float x, float y, uint seed)
        {
            float value = 0;
            float amplitude = 1;
            float frequency = 1;
            float maxValue = 0;
            
            for (int i = 0; i < 3; i++)
            {
                value += PseudoRandom(x * frequency + seed, y * frequency + seed) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2;
            }
            
            return (value / maxValue + 1) * 0.5f;
        }
        
        private float PseudoRandom(float x, float y)
        {
            int n = (int)(x * 1619 + y * 31337);
            n = (n << 13) ^ n;
            return (1.0f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f);
        }
    }
}
