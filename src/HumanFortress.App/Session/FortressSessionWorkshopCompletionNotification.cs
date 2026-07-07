using SadRogue.Primitives;

namespace HumanFortress.App.Session;

internal readonly record struct FortressSessionWorkshopCompletionNotification(
    int ChunkX,
    int ChunkY,
    int ChunkZ,
    Rectangle Footprint,
    string ConstructionId,
    ulong SimulationTick);
