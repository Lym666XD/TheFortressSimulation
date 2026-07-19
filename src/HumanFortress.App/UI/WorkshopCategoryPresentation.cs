using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.UI;

internal static class WorkshopCategoryPresentation
{
    private static readonly string[] ShortcutLabels = { "Z", "X", "C", "V", "F" };

    internal static IReadOnlyList<WorkshopCategoryView> GetCategories(
        SimulationBuildCatalogData buildCatalog) =>
        buildCatalog.WorkshopCategories?
            .OrderBy(static category => category.Id, StringComparer.Ordinal)
            .ToArray()
        ?? Array.Empty<WorkshopCategoryView>();

    internal static WorkshopCategoryView? FindCategory(
        SimulationBuildCatalogData buildCatalog,
        string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return null;

        foreach (var category in GetCategories(buildCatalog))
        {
            if (string.Equals(category.Id, categoryId, StringComparison.Ordinal))
                return category;
        }

        return null;
    }

    internal static string GetShortcutLabel(int categoryIndex) =>
        categoryIndex >= 0 && categoryIndex < ShortcutLabels.Length
            ? ShortcutLabels[categoryIndex]
            : "?";
}
