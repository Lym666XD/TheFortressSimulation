using HumanFortress.Core.World;

namespace HumanFortress.WorldGen.Implementation
{
    internal interface IWorldGenStage
    {
        string Name { get; }
        void Execute(WorldGenContext context);
    }
    
    internal sealed class WorldGenContext
    {
        internal WorldParams Params { get; set; }
        internal WorldTile[,] Tiles { get; set; }
        internal uint Seed { get; set; }
        internal int Width => Tiles.GetLength(0);
        internal int Height => Tiles.GetLength(1);
        
        internal WorldGenContext(WorldParams parameters)
        {
            Params = parameters;
            Tiles = new WorldTile[parameters.Width, parameters.Height];
            Seed = parameters.Seed;
        }
        
        internal uint GetStageSeed(string stageName)
        {
            unchecked
            {
                uint hash = Seed;
                foreach (char c in stageName)
                {
                    hash = hash * 31 + (uint)c;
                }
                return hash;
            }
        }
    }
}
