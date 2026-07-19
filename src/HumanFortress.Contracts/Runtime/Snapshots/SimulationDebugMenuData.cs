namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct DebugWorldStatusView(
    bool HasWorld,
    int ChunksLoaded,
    int ItemInstances,
    int ItemDefinitions,
    int CreatureInstances,
    int CreatureDefinitions);

public readonly record struct DebugItemView(
    string Id,
    string DisplayName);

public readonly record struct DebugCreatureView(
    string Id,
    string DisplayName);

public readonly record struct DebugItemCategoryView(
    string CategoryId,
    IReadOnlyList<DebugItemView> Items);

public readonly record struct SimulationDebugMenuData(
    DebugWorldStatusView WorldStatus,
    IReadOnlyList<DebugItemCategoryView> ItemCategories,
    IReadOnlyList<DebugCreatureView> Creatures)
{
    public SimulationDebugMenuData(
        DebugWorldStatusView worldStatus,
        IReadOnlyList<DebugItemCategoryView> itemCategories)
        : this(worldStatus, itemCategories, Array.Empty<DebugCreatureView>())
    {
    }
}

public readonly record struct SimulationDebugSpawnData(
    bool HasWorld,
    int ItemDefinitions,
    int CreatureDefinitions)
{
    public static SimulationDebugSpawnData Empty { get; } = new(false, 0, 0);
}
