using System;
using System.Collections.Generic;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Core.World;
using HumanFortress.WorldGen.Implementation.Stages;

namespace HumanFortress.WorldGen.Implementation
{
    internal sealed class WorldGenerator
    {
        private readonly List<IWorldGenStage> _stages;
        private readonly IDiagnosticSink? _diagnostics;
        internal event Action<string, float>? ProgressChanged;
        
        internal WorldGenerator(IDiagnosticSink? diagnostics = null)
        {
            _diagnostics = diagnostics;
            _stages = new List<IWorldGenStage>
            {
                new ElevationStage(),
                new ClimateStage(),
                new BiomeStage()
            };
        }
        
        internal WorldGenResult Generate(WorldParams parameters)
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
                    string errorMessage = $"Stage '{stage.Name}' failed: {ex.Message}";
                    Diagnostics.Error(
                        "WorldGen.Generator",
                        errorMessage,
                        ex);

                    return new WorldGenResult
                    {
                        Success = false,
                        Tiles = context.Tiles,
                        Params = parameters,
                        ErrorMessage = errorMessage
                    };
                }
            }
            
            ProgressChanged?.Invoke("Complete", 1.0f);
            
            return new WorldGenResult
            {
                Success = true,
                Tiles = context.Tiles,
                Params = parameters,
                ErrorMessage = string.Empty
            };
        }

        private IDiagnosticSink Diagnostics => _diagnostics ?? DiagnosticHub.Sink;
    }
    
    internal struct WorldGenResult
    {
        internal bool Success { get; set; }
        internal WorldTile[,] Tiles { get; set; }
        internal WorldParams Params { get; set; }
        internal string ErrorMessage { get; set; }
    }
}
