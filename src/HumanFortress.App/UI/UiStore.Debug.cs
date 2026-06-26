using System.Collections.Generic;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    public bool HelpOpen { get; private set; } = false;
    public bool DebugOpen { get; private set; } = false;
    public bool PauseOpen { get; private set; } = false;
    public readonly List<(Point pos, int z)> DebugDwarfs = new();

    public int DebugMenuTab { get; set; } = 0;
    public string DebugSelectedCreature { get; set; } = "core_race_dwarf";
    public string DebugSelectedItem { get; set; } = "core_item_boulder_granite";
    public DebugItemCategory DebugItemCat { get; set; } = DebugItemCategory.Boulders;
    public int DebugItemPage { get; set; } = 0;

    public void ToggleHelp()
    {
        HelpOpen = !HelpOpen;
    }

    public void ToggleDebug()
    {
        DebugOpen = !DebugOpen;
    }

    public void TogglePause()
    {
        PauseOpen = !PauseOpen;
    }

    public void AddDebugDwarf(Point p, int z)
    {
        DebugDwarfs.Add((p, z));
    }
}
