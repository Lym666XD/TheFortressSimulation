using HumanFortress.Core.World;

namespace HumanFortress.WorldGen
{
    public interface IWorldGenStage
    {
        string Name { get; }
        void Execute(WorldGenContext context);
    }
    
    public class WorldGenContext
    {
        public WorldParams Params { get; set; }
        public WorldTile[,] Tiles { get; set; }
        public uint Seed { get; set; }
        public int Width => Tiles.GetLength(0);
        public int Height => Tiles.GetLength(1);
        
        public WorldGenContext(WorldParams parameters)
        {
            Params = parameters;
            Tiles = new WorldTile[parameters.Width, parameters.Height];
            Seed = parameters.Seed;
        }
        
        public uint GetStageSeed(string stageName)
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