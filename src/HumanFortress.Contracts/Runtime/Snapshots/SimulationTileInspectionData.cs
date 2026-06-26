namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct TileInspectionItemView(
    string DisplayName,
    int StackCount);

public readonly record struct TileInspectionCreatureView(
    string DisplayName,
    int HP,
    int MaxHP);

public readonly record struct SimulationTileInspectionData(
    bool HasTile,
    int X,
    int Y,
    int Z,
    string TerrainKind,
    string GeologyLabel,
    bool IsNatural,
    bool IsModifiable,
    bool HasMud,
    bool HasGrass,
    bool HasSnow,
    int Fertility,
    string FluidKind,
    int FluidDepth,
    bool IsRevealed,
    bool IsForbidden,
    int TrafficLevel,
    bool HasBlood,
    IReadOnlyList<TileInspectionItemView> Items,
    IReadOnlyList<TileInspectionCreatureView> Creatures);
