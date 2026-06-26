using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    public string? SelectedCreatureGuid { get; set; } = null;
    public string? SelectedItemGuid { get; set; } = null;
    public string ItemKindFilter { get; set; } = "all";
    public Point? HoverTile { get; private set; } = null;

    public void SetHover(Point p)
    {
        HoverTile = p;
    }
}
