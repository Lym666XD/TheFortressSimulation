namespace HumanFortress.Contracts.WorldGen;

public enum WorldGenerationDifficulty
{
    Easy,
    Normal,
    Hard,
    Nightmare
}

public struct WorldGenerationSettings
{
    public string Name { get; set; }
    public uint Seed { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public WorldGenerationDifficulty Difficulty { get; set; }

}
