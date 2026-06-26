namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct BuildableConstructionView(
    string Id,
    string Name,
    string Category,
    int FootprintW,
    int FootprintD,
    int FootprintH,
    string Passability,
    IReadOnlyList<string> Tags);

public readonly record struct SimulationBuildCatalogData(
    IReadOnlyList<BuildableConstructionView> Workshops);
