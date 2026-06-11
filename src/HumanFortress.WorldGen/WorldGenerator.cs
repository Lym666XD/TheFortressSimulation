using System;
using System.Collections.Generic;
using HumanFortress.Core.Diagnostics;
using HumanFortress.Core.World;
using HumanFortress.WorldGen.Stages;

namespace HumanFortress.WorldGen
{
    public class WorldGenerator
    {
        private readonly List<IWorldGenStage> _stages;
        public event Action<string, float>? ProgressChanged;
        
        public WorldGenerator()
        {
            _stages = new List<IWorldGenStage>
            {
                new ElevationStage(),
                new ClimateStage(),
                new BiomeStage()
            };
        }
        
        public WorldGenResult Generate(WorldParams parameters)
        {
            var context = new WorldGenContext(parameters);
            
            for (int i = 0; i < _stages.Count; i++)
            {
                var stage = _stages[i];
                ProgressChanged?.Invoke($"Generating {stage.Name}...", (float)i / _stages.Count);
                
                try
                {
                    stage.Execute(context);
                }
                catch (Exception ex)
                {
                    DiagnosticHub.Sink.Error(
                        "WorldGen.Generator",
                        $"Error in stage {stage.Name}: {ex.Message}",
                        ex);

                    if (!DiagnosticHub.IsConfigured)
                    {
                        Console.WriteLine($"Error in stage {stage.Name}: {ex.Message}");
                    }
                }
            }
            
            ProgressChanged?.Invoke("Complete", 1.0f);
            
            return new WorldGenResult
            {
                Success = true,
                Tiles = context.Tiles,
                Params = parameters
            };
        }
    }
    
    public struct WorldGenResult
    {
        public bool Success { get; set; }
        public WorldTile[,] Tiles { get; set; }
        public WorldParams Params { get; set; }
        public string ErrorMessage { get; set; }
    }
}
