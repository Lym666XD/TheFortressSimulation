namespace HumanFortress.Simulation.Replay;

internal readonly record struct WorldReplaySectionHashes(
    string TerrainHash,
    string ItemsHash,
    string CreaturesHash,
    string ReservationsHash,
    string StockpileZonesHash,
    string PlaceablesHash,
    string OrdersHash);
