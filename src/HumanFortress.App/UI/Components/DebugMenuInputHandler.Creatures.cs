using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class DebugMenuInputHandler
{
    private bool HandleCreatureTabClick(UiStore ui, Rectangle win, Point localPos)
    {
        string[] labels = { "Dwarf", "Human", "Goblin", "Elf", "Orc" };
        var hits = DebugLayoutCalculator.CalculateCategoryPills(win, labels);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].Contains(localPos))
                continue;

            ui.DebugSelectedCreature = i switch
            {
                0 => "core_race_dwarf",
                1 => "core_race_human",
                2 => "core_race_goblin",
                3 => "core_race_elf",
                _ => "core_race_orc"
            };
            _addToast($"Creature: {labels[i]}", 50);
            return true;
        }

        return true;
    }
}
