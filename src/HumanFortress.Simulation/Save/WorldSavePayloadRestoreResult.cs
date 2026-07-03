using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Save;

internal sealed class WorldSavePayloadRestoreResult
{
    internal WorldSavePayloadRestoreResult(
        bool success,
        SimulationWorld? world,
        string savedWorldHash,
        string restoredWorldHash,
        int restoredChunkCount,
        int restoredTileCount,
        IReadOnlyList<string> issues)
    {
        Success = success;
        World = world;
        SavedWorldHash = savedWorldHash;
        RestoredWorldHash = restoredWorldHash;
        RestoredChunkCount = restoredChunkCount;
        RestoredTileCount = restoredTileCount;
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }

    internal bool Success { get; }
    internal SimulationWorld? World { get; }
    internal string SavedWorldHash { get; }
    internal string RestoredWorldHash { get; }
    internal int RestoredChunkCount { get; }
    internal int RestoredTileCount { get; }
    internal IReadOnlyList<string> Issues { get; }
}
