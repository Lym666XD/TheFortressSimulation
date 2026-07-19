using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.Runtime.Snapshots;

internal static class BuildCatalogSnapshotBuilder
{
    internal static SimulationBuildCatalogData Build(
        IConstructionCatalog? constructions,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? workshopCategoryTags = null)
    {
        if (constructions == null)
        {
            return new SimulationBuildCatalogData(
                Array.Empty<BuildableConstructionView>(),
                Array.Empty<ConstructionMaterialOptionView>(),
                Array.Empty<WorkshopCategoryView>());
        }

        var workshops = constructions.GetConstructionsByCategory("workshop").ToList();
        if (workshops.Count == 0)
            workshops = constructions.GetConstructionsByCategory("workshops").ToList();

        if (workshops.Count == 0)
        {
            workshops = constructions.GetAllConstructions()
                .Where(WorkshopSnapshotRules.IsWorkshopDefinition)
                .ToList();
        }

        var workshopViews = workshops
            .Select(WorkshopSnapshotRules.ToBuildableConstructionView)
            .OrderBy(static workshop => workshop.Id, StringComparer.Ordinal)
            .ToArray();
        return new SimulationBuildCatalogData(
            Array.AsReadOnly(workshopViews),
            BuildConstructionMaterialOptions(constructions),
            BuildWorkshopCategories(workshopViews, workshopCategoryTags));
    }

    private static IReadOnlyList<WorkshopCategoryView> BuildWorkshopCategories(
        IReadOnlyList<BuildableConstructionView> workshops,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? workshopCategoryTags)
    {
        if (workshopCategoryTags == null || workshopCategoryTags.Count == 0)
            return Array.Empty<WorkshopCategoryView>();

        var categories = workshopCategoryTags
            .OrderBy(static category => category.Key, StringComparer.Ordinal)
            .Select(category =>
            {
                var tags = category.Value.ToHashSet(StringComparer.Ordinal);
                var matches = workshops
                    .Where(workshop => workshop.Tags.Any(tags.Contains))
                    .OrderBy(static workshop => workshop.Id, StringComparer.Ordinal)
                    .ToArray();
                return new WorkshopCategoryView(
                    category.Key,
                    FormatCategoryName(category.Key),
                    Array.AsReadOnly(matches));
            })
            .ToArray();
        return Array.AsReadOnly(categories);
    }

    private static string FormatCategoryName(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return string.Empty;

        var words = categoryId.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(static word =>
            char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static IReadOnlyList<ConstructionMaterialOptionView> BuildConstructionMaterialOptions(
        IConstructionCatalog constructions)
    {
        var options = constructions.GetAllConstructions()
            .Select(static definition => TryBuildMaterialOption(definition, out var option)
                ? option
                : (ConstructionMaterialOptionView?)null)
            .Where(static option => option.HasValue)
            .Select(static option => option!.Value)
            .OrderBy(static option => option.Shape)
            .ThenBy(static option => option.Id, StringComparer.Ordinal)
            .ToArray();
        return Array.AsReadOnly(options);
    }

    private static bool TryBuildMaterialOption(
        ConstructionDefinition definition,
        out ConstructionMaterialOptionView option)
    {
        if (!TryMapShape(definition.Category, out var shape))
        {
            option = default;
            return false;
        }

        var requirements = definition.MaterialCosts
            .Where(static cost =>
                !string.IsNullOrWhiteSpace(cost.Tag)
                || !string.IsNullOrWhiteSpace(cost.DefId))
            .Select(static cost => new RuntimeConstructionMaterialRequirement(
                cost.Tag,
                cost.DefId,
                Math.Max(1, cost.Count)))
            .OrderBy(static requirement => requirement.Tag ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static requirement => requirement.DefinitionId ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        if (requirements.Length == 0)
        {
            option = default;
            return false;
        }

        option = new ConstructionMaterialOptionView(
            definition.Id,
            definition.Name,
            shape,
            definition.ResultMaterialId,
            Array.AsReadOnly(requirements));
        return true;
    }

    private static bool TryMapShape(string category, out RuntimeConstructionShape shape)
    {
        shape = category.ToLowerInvariant() switch
        {
            "wall" or "walls" => RuntimeConstructionShape.Wall,
            "floor" or "floors" => RuntimeConstructionShape.Floor,
            "ramp" or "ramps" => RuntimeConstructionShape.Ramp,
            "stair" or "stairs" => RuntimeConstructionShape.Stairs,
            _ => default
        };
        return category.ToLowerInvariant() is "wall" or "walls"
            or "floor" or "floors"
            or "ramp" or "ramps"
            or "stair" or "stairs";
    }
}
