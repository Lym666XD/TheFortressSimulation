using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

internal sealed record StockpileCreateZoneData(
    string Name,
    ChunkKey HomeChunk,
    ulong CreatedTick,
    IReadOnlyDictionary<ChunkKey, IReadOnlyList<int>> CellsByChunk,
    StockpileFilter Filter,
    int ZonePriority);

internal sealed record StockpileItemIndexData(ItemStackRef Stack);
