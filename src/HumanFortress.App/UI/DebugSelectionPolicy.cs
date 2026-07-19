using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.UI;

internal static class DebugSelectionPolicy
{
    internal static void EnsureValidSelections(UiStore ui, SimulationDebugMenuData debugMenu)
    {
        ArgumentNullException.ThrowIfNull(ui);

        var creatures = debugMenu.Creatures ?? Array.Empty<DebugCreatureView>();
        if (!creatures.Any(creature =>
                string.Equals(creature.Id, ui.DebugSelectedCreature, StringComparison.Ordinal)))
        {
            ui.DebugSelectedCreature = creatures.FirstOrDefault().Id ?? string.Empty;
        }

        var items = (debugMenu.ItemCategories ?? Array.Empty<DebugItemCategoryView>())
            .SelectMany(static category => category.Items ?? Array.Empty<DebugItemView>())
            .ToArray();
        if (!items.Any(item => string.Equals(item.Id, ui.DebugSelectedItem, StringComparison.Ordinal)))
            ui.DebugSelectedItem = items.FirstOrDefault().Id ?? string.Empty;
    }

    internal static bool SelectCreatureByIndex(
        UiStore ui,
        SimulationDebugMenuData debugMenu,
        int index)
    {
        var creatures = debugMenu.Creatures ?? Array.Empty<DebugCreatureView>();
        if (index < 0 || index >= creatures.Count)
            return false;

        ui.DebugSelectedCreature = creatures[index].Id;
        return true;
    }
}
