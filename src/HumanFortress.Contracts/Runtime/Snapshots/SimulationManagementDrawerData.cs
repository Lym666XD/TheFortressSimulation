namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct SimulationManagementDrawerData(
    bool HasWorld,
    CreatureDrawerData Creatures,
    ItemDrawerData Items,
    ZoneDrawerData Zones,
    StockpileDrawerData Stockpiles)
{
    public static SimulationManagementDrawerData Empty { get; } = new(
        false,
        CreatureDrawerData.Empty,
        ItemDrawerData.Empty,
        ZoneDrawerData.Empty,
        StockpileDrawerData.Empty);
}

public readonly record struct CreatureDrawerData(
    IReadOnlyList<CreatureDrawerRowView> Rows,
    int Total,
    int Alive,
    int Dead)
{
    public static CreatureDrawerData Empty { get; } = new(
        Array.Empty<CreatureDrawerRowView>(),
        0,
        0,
        0);
}

public readonly record struct CreatureDrawerRowView(
    Guid CreatureId,
    string DisplayName,
    string FactionId,
    bool Alive,
    int HP,
    int MaxHP,
    int X,
    int Y,
    int Z);

public readonly record struct ItemDrawerData(
    IReadOnlyList<ItemDrawerRowView> GroundItems,
    IReadOnlyList<string> AvailableKinds,
    int TotalItems,
    int TotalUnits)
{
    public static ItemDrawerData Empty { get; } = new(
        Array.Empty<ItemDrawerRowView>(),
        new[] { "all" },
        0,
        0);
}

public readonly record struct ItemDrawerRowView(
    Guid ItemId,
    string DefinitionId,
    string DisplayName,
    string Kind,
    int StackCount,
    int X,
    int Y,
    int Z);

public readonly record struct ZoneDrawerData(IReadOnlyList<ZoneDrawerRowView> Zones)
{
    public static ZoneDrawerData Empty { get; } = new(Array.Empty<ZoneDrawerRowView>());
}

public readonly record struct ZoneDrawerRowView(
    int ZoneId,
    string Name,
    string DefinitionId,
    string DisplayName,
    int TotalCells,
    int Priority,
    bool Enabled);

public readonly record struct StockpileDrawerData(IReadOnlyList<StockpileDrawerRowView> Stockpiles)
{
    public static StockpileDrawerData Empty { get; } = new(Array.Empty<StockpileDrawerRowView>());
}

public readonly record struct StockpileDrawerRowView(
    int ZoneId,
    string Name,
    int Priority,
    int TargetStacks,
    int HysteresisLow,
    int HysteresisHigh);
