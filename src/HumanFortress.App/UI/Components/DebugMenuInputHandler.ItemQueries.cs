using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.UI.Components;

internal sealed partial class DebugMenuInputHandler
{
    private static IEnumerable<string> GetCategoryItemIds(SimulationDebugMenuData debugMenu, DebugItemCategory cat)
    {
        var categoryId = cat.ToString();
        var category = debugMenu.ItemCategories?
            .FirstOrDefault(candidate => string.Equals(candidate.CategoryId, categoryId, StringComparison.Ordinal)) ?? default;
        return string.IsNullOrWhiteSpace(category.CategoryId)
            ? Array.Empty<string>()
            : (category.Items ?? Array.Empty<DebugItemView>()).Select(item => item.Id);
    }
}
