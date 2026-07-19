using HumanFortress.Contracts.Runtime;

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

public readonly record struct ConstructionMaterialOptionView(
    string Id,
    string Name,
    RuntimeConstructionShape Shape,
    string? ResultMaterialId,
    IReadOnlyList<RuntimeConstructionMaterialRequirement> Requirements);

public readonly record struct WorkshopCategoryView(
    string Id,
    string DisplayName,
    IReadOnlyList<BuildableConstructionView> Workshops);

public readonly record struct SimulationBuildCatalogData(
    IReadOnlyList<BuildableConstructionView> Workshops,
    IReadOnlyList<ConstructionMaterialOptionView> ConstructionMaterialOptions,
    IReadOnlyList<WorkshopCategoryView> WorkshopCategories)
{
    public SimulationBuildCatalogData(IReadOnlyList<BuildableConstructionView> workshops)
        : this(
            workshops,
            Array.Empty<ConstructionMaterialOptionView>(),
            Array.Empty<WorkshopCategoryView>())
    {
    }

    public SimulationBuildCatalogData(
        IReadOnlyList<BuildableConstructionView> workshops,
        IReadOnlyList<ConstructionMaterialOptionView> constructionMaterialOptions)
        : this(workshops, constructionMaterialOptions, Array.Empty<WorkshopCategoryView>())
    {
    }
}
