using System.Collections.Generic;

namespace HumanFortress.Core.World
{
    public struct WorldParams
    {
        public string Name { get; set; }
        public uint Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DifficultyPreset Difficulty { get; set; }
        
        public static WorldParams Default => new WorldParams
        {
            Name = "World",
            Seed = 1,
            Width = 256,
            Height = 256,
            Difficulty = DifficultyPreset.Normal
        };
    }
    
    public enum DifficultyPreset
    {
        Easy,
        Normal,
        Hard,
        Nightmare
    }
    
    public struct FortressParams
    {
        public int ChunkWidth { get; set; }
        public int ChunkHeight { get; set; }
        public int ChunkZ { get; set; }
        public IReadOnlyList<int> SizeOptions { get; set; }

        public static FortressParams Default => new FortressParams
        {
            ChunkWidth = 32,
            ChunkHeight = 32,
            ChunkZ = 50,
            SizeOptions = new List<int> { 1, 2, 3, 4 }
        };
    }
}
