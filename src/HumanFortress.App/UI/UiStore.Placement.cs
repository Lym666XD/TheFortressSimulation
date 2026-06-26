using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    public PlacementMode PlaceMode { get; set; } = PlacementMode.None;
    public Point? PlaceFirstCorner { get; set; } = null;
    public Point? PlaceSecondCorner { get; set; } = null;
    public int PlaceZ { get; set; } = 0;
    public int PlaceZMin { get; set; } = 0;
    public int PlaceZMax { get; set; } = 0;
    public string? CopiedPreset { get; set; } = null;
    public int? CopiedPriority { get; set; } = null;
    public string? SelectedZoneDefId { get; set; } = null;
    public bool ShowIneligibleHints { get; set; } = true;

    public void StartPlacement(PlacementMode mode, int z)
    {
        PlaceMode = mode;
        PlaceZ = z;
        PlaceZMin = z;
        PlaceZMax = z;
        PlaceFirstCorner = null;
        PlaceSecondCorner = null;
        Context = UiContext.PlacingTool;
    }

    public void CancelPlacement()
    {
        PlaceMode = PlacementMode.None;
        PlaceFirstCorner = null;
        PlaceSecondCorner = null;
        PlaceZMin = 0;
        PlaceZMax = 0;
        QuickMenu = QuickMenuKind.None;
        OrdersMenu = OrdersSubmenu.None;
        ZoneMenu = ZoneSubmenu.None;
        BuildMenu = BuildSubmenu.None;
        StockpileMenu = StockpileSubmenu.None;
        OpenDrawer = DrawerId.None;
        Context = UiContext.Global;
    }
}
