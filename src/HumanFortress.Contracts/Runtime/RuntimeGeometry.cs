namespace HumanFortress.Contracts.Runtime;

public readonly record struct RuntimePoint(int X, int Y);

public readonly record struct RuntimeRect(int X, int Y, int Width, int Height);

public readonly record struct RuntimeWorkshopCompletionNotification(
    int ChunkX,
    int ChunkY,
    int ChunkZ,
    RuntimeRect Footprint,
    string ConstructionId,
    ulong Tick);
