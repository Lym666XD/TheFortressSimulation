namespace HumanFortress.Contracts.WorldGen;

public readonly record struct WorldTileSnapshot(
    ushort BiomeId,
    string BiomeName,
    float Elevation,
    float Temperature,
    float Rainfall,
    float Drainage,
    byte RiverClass,
    bool HasAquifer,
    IReadOnlyList<ushort> StoneSet,
    IReadOnlyList<int> LandmarkIds);
