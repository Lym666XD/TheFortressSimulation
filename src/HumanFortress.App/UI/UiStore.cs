using SadRogue.Primitives;

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
    MiningSecondCorner
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

    // Stockpile placement state
    public Point? PlaceFirstCorner { get; set; } = null;
    public Point? PlaceSecondCorner { get; set; } = null;
    public int PlaceZ { get; set; } = 0;
    public string? CopiedPreset { get; set; } = null;
    public int? CopiedPriority { get; set; } = null;
    public bool HelpOpen { get; private set; } = false;
    public bool DebugOpen { get; private set; } = false;
    public bool PauseOpen { get; private set; } = false;
    public readonly List<(Point pos, int z)> DebugDwarfs = new();

    // Debug menu state
    public int DebugMenuTab { get; set; } = 0; // 0=Status, 1=Creatures, 2=Items
    public string DebugSelectedCreature { get; set; } = "core_race_dwarf";
    public string DebugSelectedItem { get; set; } = "core_item_stone_generic";

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

    public void OpenOrdersSubmenu(OrdersSubmenu submenu)
    {
        OrdersMenu = submenu;
    }

    public void OpenBuildSubmenu(BuildSubmenu submenu)
    {
        BuildMenu = submenu;
    }

    public void OpenStockpileSubmenu(StockpileSubmenu submenu)
    {
        StockpileMenu = submenu;
    }

    public void StartPlacement(PlacementMode mode, int z)
    {
        PlaceMode = mode;
        PlaceZ = z;
        PlaceFirstCorner = null;
        PlaceSecondCorner = null;
        Context = UiContext.PlacingTool;
    }

    public void CancelPlacement()
    {
        PlaceMode = PlacementMode.None;
        PlaceFirstCorner = null;
        PlaceSecondCorner = null;
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

    public void PruneToasts(ulong nowTick)
    {
        for (int i = Toasts.Count - 1; i >= 0; i--)
        {
            if (Toasts[i].expireTick <= nowTick)
                Toasts.RemoveAt(i);
        }
    }
}
