using SadRogue.Primitives;
using System.Collections.Generic;

namespace HumanFortress.App.UI;

public enum UiContext
{
    Global,
    Drawer,
    QuickMenu,
    PlacingTool
}

public enum DrawerId
{
    None,
    Creature,
    Stock,
    PlacementManagement,
    Work,
    Military,
    Country,
    World,
    Log
}

public enum QuickMenuKind
{
    None,
    Orders,
    Zones,
    Build,
    Stockpile
}

public enum OrdersSubmenu
{
    None,
    Mining,
    Lumbering,
    Gather,
    Masonry,
    Haul,
    Creature,
    Other
}

public enum ZoneSubmenu
{
    None,
    Production,
    Civil,
    Public,
    Military,
    Management
}

public enum BuildSubmenu
{
    None,
    Structural,
    FunctionalStructure,
    Workshop,
    CivilFurniture,
    UtilityFurniture
}

public enum StockpileSubmenu
{
    None,
    Stockpile
}

public enum PlacementMode
{
    None,
    // Stockpiles
    StockpileFirstCorner,
    StockpileSecondCorner,
    StockpilePresetSelect,
    StockpileDelete,
    StockpileCopy,
    // Orders
    HaulFirstCorner,
    HaulSecondCorner,
    MiningFirstCorner,
    MiningSecondCorner,
    // Build (Construction)
    ConstructionFirstCorner,
    ConstructionSecondCorner,
    // Buildable (L2 placeables)
    BuildableFirstAnchor,
    BuildableConfirmAnchor,
    // Zones
    ZoneFirstCorner,
    ZoneSecondCorner,
    ZoneDelete
}

public sealed class UiStore
{
    public UiContext Context { get; private set; } = UiContext.Global;
    public DrawerId OpenDrawer { get; private set; } = DrawerId.None;
    public int DrawerTab { get; private set; } = 0;
    public QuickMenuKind QuickMenu { get; private set; } = QuickMenuKind.None;

    // Creature/Item tracking state
    public string? SelectedCreatureGuid { get; set; } = null;
    public string? SelectedItemGuid { get; set; } = null;
    public string ItemKindFilter { get; set; } = "all"; // all/resource/weapon/armor/tool/container/consumable
    public ZoneSubmenu ZoneMenu { get; private set; } = ZoneSubmenu.None;
    public OrdersSubmenu OrdersMenu { get; private set; } = OrdersSubmenu.None;
    public BuildSubmenu BuildMenu { get; private set; } = BuildSubmenu.None;
    public StockpileSubmenu StockpileMenu { get; private set; } = StockpileSubmenu.None;
    public PlacementMode PlaceMode { get; set; } = PlacementMode.None;
    public Point? HoverTile { get; private set; } = null;

    // Build/Construction selection state
    public HumanFortress.Simulation.Orders.ConstructionShape SelectedConstructionShape { get; set; } = HumanFortress.Simulation.Orders.ConstructionShape.Wall;
    public MiningAction SelectedMiningAction { get; set; } = MiningAction.Dig;
    // Buildable selection
    public string? SelectedBuildableConstructionId { get; set; } = null;
    public string? SelectedWorkshopCategory { get; set; } = null; // mining/lumbering/farming/industry/crafts
    public bool WorkshopBrowsingItems { get; set; } = false;
    // Construction material selection (UI dialog)
    public bool ConstructionMaterialDialogOpen { get; set; } = false;
    public List<string> ConstructionSelectedTags { get; } = new();
    public string? ConstructionPreferredMaterialId { get; set; } = null;
    public void ResetConstructionSelection()
    {
        ConstructionSelectedTags.Clear();
        ConstructionPreferredMaterialId = null;
    }
    public void ResetBuildableSelection()
    {
        SelectedBuildableConstructionId = null;
    }

    public void ResetWorkshopMenu()
    {
        SelectedWorkshopCategory = null;
        WorkshopBrowsingItems = false;
    }

    // === Workshop panel state ===
    public bool WorkshopPanelOpen { get; private set; } = false;
    public Guid? OpenWorkshopGuid { get; private set; } = null;
    public SadRogue.Primitives.Point OpenWorkshopAnchor { get; private set; } = new SadRogue.Primitives.Point(0,0);
    public int OpenWorkshopZ { get; private set; } = 0;

    public void OpenWorkshopPanel(Guid guid, SadRogue.Primitives.Point anchor, int z)
    {
        OpenWorkshopGuid = guid;
        OpenWorkshopAnchor = anchor;
        OpenWorkshopZ = z;
        WorkshopPanelOpen = true;
    }

    public void CloseWorkshopPanel()
    {
        WorkshopPanelOpen = false;
        OpenWorkshopGuid = null;
    }

    // Stockpile/Zone placement state
    public Point? PlaceFirstCorner { get; set; } = null;
    public Point? PlaceSecondCorner { get; set; } = null;
    public int PlaceZ { get; set; } = 0;
    public int PlaceZMin { get; set; } = 0;
    public int PlaceZMax { get; set; } = 0;
    public string? CopiedPreset { get; set; } = null;
    public int? CopiedPriority { get; set; } = null;

    // Zone placement state
    public string? SelectedZoneDefId { get; set; } = null;
    public bool HelpOpen { get; private set; } = false;
    public bool DebugOpen { get; private set; } = false;
    public bool PauseOpen { get; private set; } = false;
    public readonly List<(Point pos, int z)> DebugDwarfs = new();

    // Debug menu state
    public int DebugMenuTab { get; set; } = 0; // 0=Status, 1=Creatures, 2=Items
    public string DebugSelectedCreature { get; set; } = "core_race_dwarf";
    public string DebugSelectedItem { get; set; } = "core_item_boulder_granite";
    public DebugItemCategory DebugItemCat { get; set; } = DebugItemCategory.Boulders;
    public int DebugItemPage { get; set; } = 0;

    // UI hints toggle (for ineligible tile preview)
    public bool ShowIneligibleHints { get; set; } = true;

    // Toasts (text + expire tick)
    public readonly List<(string text, ulong expireTick)> Toasts = new();

    public void OpenPanel(DrawerId id)
    {
        if (OpenDrawer == id)
        {
            // toggle close
            OpenDrawer = DrawerId.None;
            Context = QuickMenu != QuickMenuKind.None ? UiContext.QuickMenu : UiContext.Global;
            return;
        }
        OpenDrawer = id;
        DrawerTab = 0;
        Context = UiContext.Drawer;
    }

    public void OpenQuickMenu(QuickMenuKind kind)
    {
        if (QuickMenu == kind)
        {
            QuickMenu = QuickMenuKind.None;
            ZoneMenu = ZoneSubmenu.None;
            if (OpenDrawer == DrawerId.None)
                Context = UiContext.Global;
            return;
        }
        QuickMenu = kind;
        Context = UiContext.QuickMenu;
    }

    public void OpenZoneSubmenu(ZoneSubmenu submenu)
    {
        ZoneMenu = submenu;
    }

    public void CloseZoneSubmenu()
    {
        ZoneMenu = ZoneSubmenu.None;
    }

    public void OpenOrdersSubmenu(OrdersSubmenu submenu)
    {
        OrdersMenu = submenu;
    }

    public void CloseOrdersSubmenu()
    {
        OrdersMenu = OrdersSubmenu.None;
    }

    public void OpenBuildSubmenu(BuildSubmenu submenu)
    {
        BuildMenu = submenu;
    }

    public void CloseBuildSubmenu()
    {
        BuildMenu = BuildSubmenu.None;
    }

    public void OpenStockpileSubmenu(StockpileSubmenu submenu)
    {
        StockpileMenu = submenu;
    }

    public void CloseStockpileSubmenu()
    {
        StockpileMenu = StockpileSubmenu.None;
    }

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

    public void TabNext()
    {
        if (Context != UiContext.Drawer) return;
        DrawerTab = (DrawerTab + 1) % 3; // three stub tabs by default
    }

    public void TabPrev()
    {
        if (Context != UiContext.Drawer) return;
        DrawerTab = (DrawerTab + 2) % 3;
    }

    public void Back()
    {
        if (Context == UiContext.Drawer)
        {
            // Close drawer
            OpenDrawer = DrawerId.None;
            Context = QuickMenu != QuickMenuKind.None ? UiContext.QuickMenu : UiContext.Global;
        }
        else if (Context == UiContext.QuickMenu)
        {
            if (OrdersMenu != OrdersSubmenu.None)
            {
                // Back from orders submenu to orders root
                OrdersMenu = OrdersSubmenu.None;
            }
            else if (ZoneMenu != ZoneSubmenu.None)
            {
                // Back from zone submenu to zone root
                ZoneMenu = ZoneSubmenu.None;
            }
            else if (BuildMenu != BuildSubmenu.None)
            {
                // Back from build submenu to build root
                BuildMenu = BuildSubmenu.None;
            }
            else if (StockpileMenu != StockpileSubmenu.None)
            {
                // Back from stockpile submenu to stockpile root
                StockpileMenu = StockpileSubmenu.None;
            }
            else
            {
                // Close quick menu
                QuickMenu = QuickMenuKind.None;
                Context = UiContext.Global;
            }
        }
        else if (Context == UiContext.PlacingTool)
        {
            CancelPlacement();
        }
        else if (HelpOpen)
        {
            HelpOpen = false;
        }
        else if (DebugOpen)
        {
            DebugOpen = false;
        }
    }

    public void Cancel()
    {
        // ESC/MouseRight always cancel to Global (global cancel behavior)
        if (Context == UiContext.PlacingTool || Context == UiContext.QuickMenu || Context == UiContext.Drawer)
        {
            CancelPlacement();
        }
        else if (HelpOpen)
        {
            HelpOpen = false;
        }
        else if (DebugOpen)
        {
            DebugOpen = false;
        }
        else if (WorkshopPanelOpen)
        {
            CloseWorkshopPanel();
        }
    }

    public void SetHover(Point p)
    {
        HoverTile = p;
    }

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

    public void AddToast(string text, ulong expireTick)
    {
        Toasts.Add((text, expireTick));
    }

    // === Recent order highlights ===
    public struct OrderHighlight
    {
        public string Kind; // mining/construction/haul/zone/stockpile
        public SadRogue.Primitives.Rectangle Rect;
        public int ZMin;
        public int ZMax;
        public ulong ExpireTick;
    }

    private readonly List<OrderHighlight> _highlights = new();

    public void AddHighlight(string kind, SadRogue.Primitives.Rectangle rect, int zMin, int zMax, ulong expireTick)
    {
        _highlights.Add(new OrderHighlight { Kind = kind, Rect = rect, ZMin = zMin, ZMax = zMax, ExpireTick = expireTick });
    }

    public IReadOnlyList<OrderHighlight> GetHighlights() => _highlights;

    public void PruneHighlights(ulong nowTick)
    {
        for (int i = _highlights.Count - 1; i >= 0; i--)
        {
            if (_highlights[i].ExpireTick <= nowTick)
                _highlights.RemoveAt(i);
        }
    }

    public void PruneToasts(ulong nowTick)
    {
        for (int i = Toasts.Count - 1; i >= 0; i--)
        {
            if (Toasts[i].expireTick <= nowTick)
                Toasts.RemoveAt(i);
        }
    }
}

public enum DebugItemCategory
{
    Boulders,
    Blocks,
    Logs,
    Planks,
    Tools,
    Weapons,
    Ammo,
    SiegeWeapons
}

public enum MiningAction
{
    Dig,
    DigStairwell,
    DigRamp,
    DigChannel,
    RemoveDigging
}
