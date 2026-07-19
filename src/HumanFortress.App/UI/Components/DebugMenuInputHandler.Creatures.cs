using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class DebugMenuInputHandler
{
    private bool HandleCreatureTabClick(
        UiStore ui,
        SimulationDebugMenuData debugMenu,
        Rectangle win,
        Point localPos)
    {
        var creatures = (debugMenu.Creatures ?? Array.Empty<DebugCreatureView>())
            .Take(5)
            .ToArray();
        var labels = creatures.Select(static creature => creature.DisplayName).ToArray();
        var hits = DebugLayoutCalculator.CalculateCategoryPills(win, labels);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].Contains(localPos))
                continue;

            ui.DebugSelectedCreature = creatures[i].Id;
            _addToast($"Creature: {labels[i]}", 50);
            return true;
        }

        return true;
    }
}
