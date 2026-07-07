using HumanFortress.Simulation.Replay;

namespace HumanFortress.Simulation.Save;

internal static class WorldSaveSnapshotSchema
{
    internal const int CurrentVersion = 1;
}

internal readonly record struct WorldSaveSnapshot(
    int SchemaVersion,
    int SizeInChunks,
    int SizeInTiles,
    int MaxZ,
    string ReplayHash,
    WorldReplaySectionHashes SectionHashes,
    WorldSaveCounts Counts);

internal readonly record struct WorldSaveCounts(
    int ChunkCount,
    int TileCount,
    int ItemCount,
    int CreatureCount,
    int ItemReservationCount,
    int CreatureReservationCount,
    int StockpileZoneCount,
    int OwnedPlaceableCount,
    int MiningOrderCount,
    int HaulOrderCount,
    int ConstructionOrderCount,
    int BuildableOrderCount);
