using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal readonly record struct FortressWorkshopCompletionNotification(
    int ChunkX,
    int ChunkY,
    int ChunkZ,
    Rectangle Footprint,
    string ConstructionId,
    ulong SimulationTick);
