namespace HumanFortress.Contracts.WorldGen;

public readonly record struct WorldMapTilePosition(int X, int Y);

public enum WorldMapBiomeDisplayKind
{
    Ocean,
    Lake,
    River,
    Mountain,
    Hills,
    Desert,
    Tundra,
    Glacier,
    TemperateForest,
    TropicalForest,
    Taiga,
    TemperateGrassland,
    Savanna,
    Swamp,
    Unknown
}

public readonly record struct WorldMapTileView(
    WorldMapTilePosition Position,
    ushort BiomeId,
    string BiomeName,
    WorldMapBiomeDisplayKind DisplayKind,
    double Elevation,
    double Temperature,
    double Rainfall,
    double Drainage,
    bool IsEmbarkable,
    IReadOnlyList<string> EmbarkabilityFailures);
