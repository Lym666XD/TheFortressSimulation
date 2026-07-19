using System;
using HumanFortress.Core.World;

namespace HumanFortress.WorldGen.Implementation.Stages
{
    internal sealed class ElevationStage : IWorldGenStage
    {
        internal string Name => "Elevation";

        string IWorldGenStage.Name => Name;
        
        internal void Execute(WorldGenContext context)
        {
            var seed = context.GetStageSeed(Name);

            for (int x = 0; x < context.Width; x++)
            {
                for (int y = 0; y < context.Height; y++)
                {
                    float elevation = GenerateElevation(x, y, context.Width, context.Height, seed);
                    context.Tiles[x, y].Elevation = elevation;
                }
            }
        }

        void IWorldGenStage.Execute(WorldGenContext context)
        {
            Execute(context);
        }

        private float GenerateElevation(int x, int y, int width, int height, uint seed)
        {
            float nx = (float)x / width;
            float ny = (float)y / height;

            float ridgedHash = RidgedFractalCoordinateHash(nx * 4, ny * 4, seed);
            float normalizedHash = NormalizedFractalCoordinateHash(nx * 8, ny * 8, seed + 1000);
            
            float elevation = (ridgedHash * 0.7f + normalizedHash * 0.3f);
            
            elevation = Math.Max(0, Math.Min(1, elevation));
            
            return elevation;
        }
        
        private static float RidgedFractalCoordinateHash(float x, float y, uint seed)
        {
            float value = 0;
            float amplitude = 1;
            float frequency = 1;
            float maxValue = 0;
            
            for (int i = 0; i < 4; i++)
            {
                float n = 1 - Math.Abs(CoordinateHashValue(x * frequency, y * frequency, seed));
                n = n * n;
                value += n * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2;
            }
            
            return value / maxValue;
        }
        
        private static float NormalizedFractalCoordinateHash(float x, float y, uint seed)
        {
            float value = 0;
            float amplitude = 1;
            float frequency = 1;
            float maxValue = 0;
            
            for (int i = 0; i < 3; i++)
            {
                value += CoordinateHashValue(x * frequency, y * frequency, seed) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2;
            }
            
            return (value / maxValue + 1) * 0.5f;
        }
        
        private static float CoordinateHashValue(float x, float y, uint seed)
        {
            // Mix the seed after quantizing the coordinates. Adding a 32-bit
            // seed to float coordinates erases their fractional differences.
            int coordinate = (int)(x * 1619 + y * 31337);
            int n = coordinate ^ unchecked((int)(seed * 6971u));
            n = (n << 13) ^ n;
            return 1.0f - unchecked(
                (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff)
                / 1073741824.0f;
        }
    }
}
