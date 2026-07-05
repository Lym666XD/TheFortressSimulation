using System;
using HumanFortress.Core.Random;

namespace HumanFortress.WorldGen.Implementation.Stages
{
    internal sealed class ClimateStage : IWorldGenStage
    {
        public string Name => "Climate";
        
        public void Execute(WorldGenContext context)
        {
            var rng = new DeterministicRng(context.GetStageSeed(Name));
            
            for (int x = 0; x < context.Width; x++)
            {
                for (int y = 0; y < context.Height; y++)
                {
                    float latitude = (float)y / context.Height;
                    float elevation = context.Tiles[x, y].Elevation;
                    
                    float temperature = CalculateTemperature(latitude, elevation);
                    temperature += (rng.NextFloat() - 0.5f) * 0.1f;
                    
                    float rainfall = CalculateRainfall(latitude, elevation, temperature);
                    rainfall += (rng.NextFloat() - 0.5f) * 0.15f;
                    
                    float drainage = elevation > 0.2f ? 0.5f + (rng.NextFloat() * 0.5f) : 0.1f;
                    
                    context.Tiles[x, y].Temperature = Math.Max(0, Math.Min(1, temperature));
                    context.Tiles[x, y].Rainfall = Math.Max(0, Math.Min(1, rainfall));
                    context.Tiles[x, y].Drainage = Math.Max(0, Math.Min(1, drainage));
                }
            }
        }
        
        private float CalculateTemperature(float latitude, float elevation)
        {
            float latTemp = 1.0f - Math.Abs(latitude - 0.5f) * 2;
            
            float altitudeLapse = elevation * 0.6f;
            
            return latTemp - altitudeLapse;
        }
        
        private float CalculateRainfall(float latitude, float elevation, float temperature)
        {
            float tropicalBelt = 1.0f - Math.Abs(latitude - 0.5f) * 4;
            tropicalBelt = Math.Max(0, tropicalBelt);
            
            float orographic = elevation > 0.5f ? elevation * 0.3f : 0;
            
            float tempEffect = temperature * 0.4f;
            
            return (tropicalBelt * 0.5f + orographic + tempEffect) * 0.7f;
        }
    }
}
