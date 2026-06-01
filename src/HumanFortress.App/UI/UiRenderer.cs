using SadConsole;
using SadRogue.Primitives;
using HumanFortress.App;
using HumanFortress.App.Runtime;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Placeables;
using System.Linq;

namespace HumanFortress.App.UI;

    public static class UiRenderer
    {
        // Draw dock icons at absolute screen bottom-left (overlay coordinates)
        public static void DrawDockScreen(ScreenSurface overlay, UiStore ui, ulong tick)
        {
            var surf = overlay.Surface;
            int y = surf.Height - 1; // move to very bottom to avoid drawer overlap
            int x = 1;
            int buttonWidth = 5; // e.g., [F1]
            // if (tick % 50 == 0)
            //     Logger.Log($"[UiRenderer.DockScreen] overlay={surf.Width}x{surf.Height} row={y}");

            DrawSquareButton(surf, ref x, y, "F1", ui.OpenDrawer == DrawerId.Creature, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F2", ui.OpenDrawer == DrawerId.Stock, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F3", ui.OpenDrawer == DrawerId.Work, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F4", ui.OpenDrawer == DrawerId.PlacementManagement, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F5", ui.OpenDrawer == DrawerId.Military, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F6", ui.OpenDrawer == DrawerId.Country, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F7", ui.OpenDrawer == DrawerId.World, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F8", ui.OpenDrawer == DrawerId.Log, buttonWidth);
        }

        // Draw dock icons aligned to a specific anchor rectangle (eg. the map surface)
        public static void DrawDockAligned(ScreenSurface overlay, UiStore ui, int anchorX, int anchorY, int anchorWidth, int anchorHeight, ulong tick)
        {
        var surf = overlay.Surface;
        int y = anchorY + anchorHeight - 2; // move up one row for visibility
        int x = anchorX;
        if (tick % 50 == 0)
            Logger.Log($"[UiRenderer.Dock] overlay={surf.Width}x{surf.Height} anchor=({anchorX},{anchorY},{anchorWidth},{anchorHeight}) row={y}");
        DrawButton(surf, ref x, y, "[F1]", ui.OpenDrawer == DrawerId.Creature);
        DrawButton(surf, ref x, y, "[F2]", ui.OpenDrawer == DrawerId.Stock);
        DrawButton(surf, ref x, y, "[F3]", ui.OpenDrawer == DrawerId.Work);
        DrawButton(surf, ref x, y, "[F4]", ui.OpenDrawer == DrawerId.PlacementManagement);
        DrawButton(surf, ref x, y, "[F5]", ui.OpenDrawer == DrawerId.Military);
        DrawButton(surf, ref x, y, "[F6]", ui.OpenDrawer == DrawerId.Country);
        DrawButton(surf, ref x, y, "[F7]", ui.OpenDrawer == DrawerId.World);
        DrawButton(surf, ref x, y, "[F8]", ui.OpenDrawer == DrawerId.Log);
    }

        // Draw quick icons aligned near bottom-center of anchor rectangle
        public static void DrawQuickIconsAligned(ScreenSurface overlay, UiStore ui, int anchorX, int anchorY, int anchorWidth, int anchorHeight, ulong tick)
        {
            var surf = overlay.Surface;
            int y = anchorY + anchorHeight - 1;
            int center = anchorX + anchorWidth / 2;
            DrawText(surf, center - 8, y, "[Z]Orders", ui.QuickMenu == QuickMenuKind.Orders);
            DrawText(surf, center + 2, y, "[X]Zones", ui.QuickMenu == QuickMenuKind.Zones);
            DrawText(surf, center + 12, y, "[C]Build", ui.QuickMenu == QuickMenuKind.Build);
            if (tick % 50 == 0)
                Logger.Log($"[UiRenderer.QuickIcons] anchor=({anchorX},{anchorY},{anchorWidth},{anchorHeight}) row={y} center={center}");
        }

        // Draw quick icons centered at the bottom row (same as F1-F7)
        public static void DrawQuickIconsScreen(ScreenSurface overlay, UiStore ui, ulong tick)
        {
            var surf = overlay.Surface;
            int y = surf.Height - 1; // bottom row, same as F1-F7
            int center = surf.Width / 2;
            int buttonWidth = 5;
            int gap = 2;

            // 4 buttons: Z X C V
            int totalWidth = (buttonWidth * 4) + (gap * 3);
            int startX = center - totalWidth / 2;

            int xZ = startX;
            int xX = startX + buttonWidth + gap;
            int xC = startX + (buttonWidth + gap) * 2;
            int xV = startX + (buttonWidth + gap) * 3;

            DrawSquareButton(surf, ref xZ, y, "Z", ui.QuickMenu == QuickMenuKind.Orders, buttonWidth);
            DrawSquareButton(surf, ref xX, y, "X", ui.QuickMenu == QuickMenuKind.Zones, buttonWidth);
            DrawSquareButton(surf, ref xC, y, "C", ui.QuickMenu == QuickMenuKind.Build, buttonWidth);
            DrawSquareButton(surf, ref xV, y, "V", ui.QuickMenu == QuickMenuKind.Stockpile, buttonWidth);

            // if (tick % 50 == 0)
            //     Logger.Log($"[UiRenderer.QuickIconsScreen] overlay={surf.Width}x{surf.Height} row={y} center={center} width={buttonWidth}");
        }

    // Draw bottom drawer placeholder
    public static void DrawDrawer(ScreenSurface mapSurface, UiStore ui, ulong tick, StockpileManager? stockpileManager = null, HumanFortress.Simulation.World.World? world = null, FortressRuntimeAccess? runtime = null)
    {
        if (ui.OpenDrawer == DrawerId.None) return;
        var surf = mapSurface.Surface;
        // Reduced height: leave 6 rows at top for map visibility, min 10 rows for drawer content
        int height = Math.Max(10, surf.Height - 7);
        int y0 = surf.Height - 1 - height; // top of drawer area
        // if (tick % 50 == 0)
        //     Logger.Log($"[UiRenderer.Drawer] size={surf.Width}x{surf.Height} height={height} y0={y0} panel={ui.OpenDrawer} tab={ui.DrawerTab}");
        // background panel
        for (int y = y0; y < surf.Height - 1; y++)
        {
            for (int x = 0; x < surf.Width; x++)
            {
                surf.SetGlyph(x, y, ' ', Color.White, new Color(20, 20, 20));
            }
        }

        // title and tabs
        string title = ui.OpenDrawer switch
        {
            DrawerId.Creature => "Creature Management",
            DrawerId.Stock => "Stock/Items Management",
            DrawerId.Work => "Work Management",
            DrawerId.PlacementManagement => "Placement Management",
            DrawerId.Military => "Military Management",
            DrawerId.Country => "Country Management",
            DrawerId.World => "World Map / Diplomacy",
            DrawerId.Log => "Log / Messages / History",
            _ => "Panel"
        };
        surf.Print(1, y0, $"== {title} ==", Color.Yellow);

        // Custom tabs per drawer
        string[] tabs;
        if (ui.OpenDrawer == DrawerId.Creature)
        {
            tabs = new[] { "All Creatures", "Animals", "Settings" };
        }
        else if (ui.OpenDrawer == DrawerId.Stock)
        {
            tabs = new[] { "Items", "Stockpiles", "Trade" };
        }
        else if (ui.OpenDrawer == DrawerId.Work)
        {
            tabs = new[] { "Labor", "All Orders", "Job Allocation", "Workshop Orders", "Workshops" };
        }
        else if (ui.OpenDrawer == DrawerId.PlacementManagement)
        {
            tabs = new[] { "Zones", "Stockpiles", "Settings" };
        }
        else
        {
            tabs = new[] { "Tab 1", "Tab 2", "Tab 3" };
        }
        int tx = 24;
        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = ui.DrawerTab == i;
            var fg = active ? Color.Black : Color.White;
            var bg = active ? Color.Yellow : new Color(50, 50, 50);
            WritePill(surf, ref tx, y0, tabs[i], fg, bg);
            tx += 1;
        }

        // Content based on drawer and tab
        if (ui.OpenDrawer == DrawerId.Creature && world != null)
        {
            if (ui.DrawerTab == 0)
                DrawCreaturesTab(surf, world, ui, y0 + 2, height - 3);
            else if (ui.DrawerTab == 1)
                DrawAnimalsTab(surf, y0 + 2);
            else
                surf.Print(2, y0 + 2, "(Settings coming soon)", Color.Gray);
        }
        else if (ui.OpenDrawer == DrawerId.Stock)
        {
            if (ui.DrawerTab == 0 && world != null)
                DrawItemsTab(surf, world, ui, y0 + 2, height - 3);
            else if (ui.DrawerTab == 1 && stockpileManager != null)
                DrawStockpilesTab(surf, stockpileManager, y0 + 2);
            else
                surf.Print(2, y0 + 2, "(Trade coming soon)", Color.Gray);
        }
        else if (ui.OpenDrawer == DrawerId.Work)
        {
            if (world == null)
            {
                surf.Print(2, y0 + 2, "(World not ready)", Color.Gray);
            }
            else
            {
                int contentHeight = height - 3;
                if (ui.DrawerTab == 0)
                {
                    DrawWorkOverviewTab(surf, world, ui, y0 + 1, contentHeight, tick, runtime);
                }
                else if (ui.DrawerTab == 1)
                {
                    DrawWorkOrdersTab(surf, world, y0 + 1, contentHeight, tick, runtime);
                }
                else if (ui.DrawerTab == 2)
                {
                    DrawJobAllocationTab(surf, world, ui, y0 + 1, contentHeight, runtime);
                }
                else if (ui.DrawerTab == 3)
                {
                    DrawWorkshopOrdersTab(surf, world, y0 + 1, contentHeight, tick, runtime);
                }
                else
                {
                    DrawWorkshopsTab(surf, world, y0 + 1, contentHeight, tick, runtime);
                }
            }
        }
        else if (ui.OpenDrawer == DrawerId.PlacementManagement && world != null)
        {
            if (ui.DrawerTab == 0)
                DrawZonesTab(surf, world, y0 + 2, height - 3);
            else if (ui.DrawerTab == 1 && stockpileManager != null)
                DrawStockpilesTab(surf, stockpileManager, y0 + 2);
            else
                surf.Print(2, y0 + 2, "(Settings coming soon)", Color.Gray);
        }
        else
        {
            // content placeholder
            surf.Print(2, y0 + 2, "(Content coming soon)", Color.Gray);
        }
    }


    private static void DrawWorkOverviewTab(ICellSurface surf, HumanFortress.Simulation.World.World world, UiStore ui, int startY, int maxHeight, ulong tick, FortressRuntimeAccess? runtime)
    {
        var layout = BuildWorkPanelLayout(surf, startY, maxHeight);
        DecorateWorkPanel(surf, layout);
        RenderLaborSummaryColumn(surf, layout.Left, world, runtime);
        RenderDwarfRosterColumn(surf, layout.Center, world, ui, runtime);
        RenderSchedulerColumn(surf, layout.Right, tick, "Scheduler Diagnostics", runtime);

        // TODO (Work panel UX #4): hook keyboard navigation between columns when input refactor lands.
    }

    private static void DrawWorkOrdersTab(ICellSurface surf, HumanFortress.Simulation.World.World world, int startY, int maxHeight, ulong tick, FortressRuntimeAccess? runtime)
    {
        var layout = BuildWorkPanelLayout(surf, startY, maxHeight);
        DecorateWorkPanel(surf, layout);
        RenderOrdersSummaryColumn(surf, layout.Left, world);
        RenderJobsColumn(surf, layout.Center, world, runtime);
        RenderSchedulerColumn(surf, layout.Right, tick, "Workshop Stats", runtime);
    }

    private static void DrawJobAllocationTab(ICellSurface surf, HumanFortress.Simulation.World.World world, UiStore ui, int startY, int maxHeight, FortressRuntimeAccess? runtime)
    {
        var service = runtime?.ProfessionAssignments;
        if (service == null)
        {
            surf.Print(2, startY + 2, "Professions unavailable.", Color.Gray);
            return;
        }
        var defs = service.Registry.Definitions;
        if (defs.Count == 0)
        {
            surf.Print(2, startY + 2, "No professions defined in registry.", Color.Gray);
            return;
        }

        var roster = runtime?.GetProfessionRosterSnapshot()
            ?? Array.Empty<HumanFortress.App.Jobs.ProfessionAssignments.ProfessionRosterEntry>();
        int areaHeight = System.Math.Max(10, maxHeight);
        var area = new SadRogue.Primitives.Rectangle(1, startY, surf.Width - 2, areaHeight);
        FillArea(surf, area, new Color(15, 15, 15));
        surf.Print(area.X + 1, area.Y, "Job Allocation (click value to cycle 1-9 / '-')", Color.Yellow);

        if (roster.Count == 0)
        {
            surf.Print(area.X + 1, area.Y + 2, "No dwarves available.", Color.DarkGray);
            return;
        }

        int nameWidth = System.Math.Max(12, area.Width / 6);
        int tableWidth = System.Math.Max(8, area.Width - nameWidth - 3);
        int colWidth = System.Math.Max(3, tableWidth / defs.Count);
        int headerY = area.Y + 1;
        int nameX = area.X + 1;
        surf.Print(nameX, headerY, "Worker".PadRight(nameWidth - 1), Color.Gray);

        for (int col = 0; col < defs.Count; col++)
        {
            int colX = nameX + nameWidth + col * colWidth;
            string label = defs[col].Name.ToUpperInvariant();
            if (label.Length > colWidth - 1)
                label = label.Substring(0, System.Math.Max(1, colWidth - 1));
            var color = col == ui.WorkAllocSelectedCol ? Color.LightCyan : Color.DarkGray;
            surf.Print(colX, headerY, label, color);
        }

        int visibleRows = System.Math.Max(1, area.Height - 4);
        ui.WorkAllocSelectedRow = System.Math.Clamp(ui.WorkAllocSelectedRow, 0, roster.Count - 1);
        ui.WorkAllocSelectedCol = System.Math.Clamp(ui.WorkAllocSelectedCol, 0, defs.Count - 1);
        int maxOffset = System.Math.Max(0, roster.Count - visibleRows);
        ui.WorkAllocRowOffset = System.Math.Clamp(ui.WorkAllocRowOffset, 0, maxOffset);
        if (ui.WorkAllocSelectedRow < ui.WorkAllocRowOffset)
            ui.WorkAllocRowOffset = ui.WorkAllocSelectedRow;
        if (ui.WorkAllocSelectedRow >= ui.WorkAllocRowOffset + visibleRows)
            ui.WorkAllocRowOffset = ui.WorkAllocSelectedRow - visibleRows + 1;

        for (int row = 0; row < visibleRows; row++)
        {
            int actual = ui.WorkAllocRowOffset + row;
            if (actual >= roster.Count) break;
            var entry = roster[actual];
            var creature = world.Creatures.GetInstance(entry.WorkerId);
            if (creature == null) continue;
            var def = world.Creatures.GetDefinition(creature.DefinitionId);
            string name = Truncate(def?.Name ?? creature.DefinitionId, nameWidth - 1);
            int rowY = headerY + 1 + row;
            surf.Print(nameX, rowY, name.PadRight(nameWidth - 1), Color.White);

            for (int col = 0; col < defs.Count; col++)
            {
                int colX = nameX + nameWidth + col * colWidth;
                string profId = defs[col].Id;
                int weight = entry.Weights.TryGetValue(profId, out var val) ? val : 0;
                string text = weight <= 0 ? "--" : weight.ToString();
                bool selected = (actual == ui.WorkAllocSelectedRow && col == ui.WorkAllocSelectedCol);
                var bg = selected ? new Color(60, 60, 0) : new Color(30, 30, 30);
                for (int i = 0; i < colWidth - 1; i++)
                    surf.SetGlyph(colX + i, rowY, ' ', Color.Black, bg);
                surf.Print(colX, rowY, text, weight <= 0 ? Color.DarkGray : Color.White);
            }
        }

        surf.Print(area.X + 1, area.Y + area.Height - 2, "Use arrows to move, click cell to cycle 1-9 / '-'", Color.DarkGray);
    }

    private static void DrawWorkshopOrdersTab(ICellSurface surf, HumanFortress.Simulation.World.World world, int startY, int maxHeight, ulong tick, FortressRuntimeAccess? runtime)
    {
        var layout = BuildWorkPanelLayout(surf, startY, maxHeight);
        DecorateWorkPanel(surf, layout);
        RenderWorkshopListColumn(surf, layout.Left, world);
        RenderWorkshopNotesColumn(surf, layout.Center, world);
        RenderSchedulerColumn(surf, layout.Right, tick, "Workshop Stats", runtime);
    }

    private static void DrawWorkshopsTab(ICellSurface surf, HumanFortress.Simulation.World.World world, int startY, int maxHeight, ulong tick, FortressRuntimeAccess? runtime)
    {
        var layout = BuildWorkPanelLayout(surf, startY, maxHeight);
        DecorateWorkPanel(surf, layout);
        RenderStandingOrdersColumn(surf, layout.Left);
        RenderWorkshopDirectory(surf, layout.Center, world);
        RenderConstructionStatusColumn(surf, layout.Right, world, runtime);
    }

    public static void DrawQuickMenu(ScreenSurface mapSurface, UiStore ui, ulong tick, OrdersUI? ordersUI = null, ZonesUI? zonesUI = null, BuildUI? buildUI = null, StockpileQuickUI? stockpileUI = null, SadRogue.Primitives.Point? cameraOverride = null, int? zOverride = null, HumanFortress.Simulation.World.World? world = null)
    {
        if (ui.QuickMenu == QuickMenuKind.None) return;
        var surf = mapSurface.Surface;
        int centerX = surf.Width / 2;

        // Button is at surf.Height - 1, menus should end at surf.Height - 2
        // For L2/L3 side-by-side menus, they should be higher up (not too low)

        // Orders menu
        if (ui.QuickMenu == QuickMenuKind.Orders && ordersUI != null)
        {
            if (ui.OrdersMenu == OrdersSubmenu.None)
            {
                // L1: height=8, y = surf.Height - 9 (ends at height-2, above button)
                int x = (surf.Width - 30) / 2;
                int y = surf.Height - 9;
                ordersUI.DrawOrdersRootPopup(mapSurface, x, y);
            }
            else
            {
                // L2+L3: height=10, place at top of L2 menu (not centerY!)
                // We want bottom at surf.Height - 2, so top is surf.Height - 2 - 10 + 1
                int l2Y = surf.Height - 11;
                ordersUI.DrawOrdersWithSubmenu(mapSurface, centerX, l2Y, ui.OrdersMenu);
            }
        }
        // Zones menu
        else if (ui.QuickMenu == QuickMenuKind.Zones && zonesUI != null)
        {
            if (ui.ZoneMenu == ZoneSubmenu.None)
            {
                // L1: height=8, y = surf.Height - 9
                int x = (surf.Width - 30) / 2;
                int y = surf.Height - 9;
                zonesUI.DrawZonesRootPopup(mapSurface, x, y);
            }
            else
            {
                // L2+L3: height=8, place at top of L2 menu
                int l2Y = surf.Height - 9;
                zonesUI.DrawZonesWithSubmenu(mapSurface, centerX, l2Y, ui.ZoneMenu);
            }
        }
        // Build menu
        else if (ui.QuickMenu == QuickMenuKind.Build && buildUI != null)
        {
            if (ui.BuildMenu == BuildSubmenu.None)
            {
                // L1: height=8, y = surf.Height - 9
                int x = (surf.Width - 30) / 2;
                int y = surf.Height - 9;
                buildUI.DrawBuildRootPopup(mapSurface, x, y);
            }
            else
            {
                // L2: height=8, place at top of L2 menu
                int l2Y = surf.Height - 9;
                buildUI.DrawBuildWithSubmenu(mapSurface, centerX, l2Y, ui.BuildMenu);

                // If browsing workshop items, draw the items pane on the right
                if (ui.BuildMenu == BuildSubmenu.Workshop && ui.WorkshopBrowsingItems && ui.SelectedWorkshopCategory != null)
                {
                    DrawWorkshopItemsPane(mapSurface, centerX + 32, l2Y, ui.SelectedWorkshopCategory);
                }
            }
        }
        // Stockpile menu
        else if (ui.QuickMenu == QuickMenuKind.Stockpile && stockpileUI != null)
        {
            if (ui.StockpileMenu == StockpileSubmenu.None)
            {
                // L1: height=6, y = surf.Height - 7
                int x = (surf.Width - 30) / 2;
                int y = surf.Height - 7;
                stockpileUI.DrawStockpileRootPopup(mapSurface, x, y);
            }
            else
            {
                // L2+L3: height=6, place at top of L2 menu
                int l2Y = surf.Height - 7;
                stockpileUI.DrawStockpileWithSubmenu(mapSurface, centerX, l2Y, ui.StockpileMenu);
            }
        }

        // Highlights are drawn by FortressState after map render to ensure visibility across UI states.
    }

    private readonly struct WorkPanelLayout
    {
        public WorkPanelLayout(Rectangle left, Rectangle center, Rectangle right)
        {
            Left = left;
            Center = center;
            Right = right;
        }

        public Rectangle Left { get; }
        public Rectangle Center { get; }
        public Rectangle Right { get; }
    }

    private readonly record struct WorkCategoryCard(string Name, string Detail, int Active, int Backlog, Color Accent);
    private readonly record struct ActiveJobRow(string Kind, string Worker, string Stage, string Target, Color Color);

    private static WorkPanelLayout BuildWorkPanelLayout(ICellSurface surf, int startY, int maxHeight)
    {
        int panelHeight = System.Math.Max(12, maxHeight);
        int leftWidth = System.Math.Max(18, surf.Width / 5);
        int rightWidth = System.Math.Max(22, surf.Width / 4);
        int centerWidth = surf.Width - leftWidth - rightWidth - 4;
        if (centerWidth < 24)
        {
            int deficit = 24 - centerWidth;
            centerWidth = 24;
            rightWidth = System.Math.Max(18, rightWidth - deficit);
        }

        var left = new Rectangle(1, startY, leftWidth, panelHeight);
        var center = new Rectangle(left.X + left.Width + 1, startY, centerWidth, panelHeight);
        int rightX = center.X + center.Width + 1;
        var right = new Rectangle(rightX, startY, System.Math.Max(18, surf.Width - rightX - 1), panelHeight);
        return new WorkPanelLayout(left, center, right);
    }

    private static void DecorateWorkPanel(ICellSurface surf, WorkPanelLayout layout)
    {
        FillArea(surf, layout.Left, new Color(32, 32, 32));
        FillArea(surf, layout.Center, new Color(18, 18, 18));
        FillArea(surf, layout.Right, new Color(32, 32, 32));
    }

    private static void FillArea(ICellSurface surf, Rectangle rect, Color bg)
    {
        int maxY = System.Math.Min(rect.Y + rect.Height, surf.Height);
        int maxX = System.Math.Min(rect.X + rect.Width, surf.Width);
        for (int y = rect.Y; y < maxY; y++)
        {
            for (int x = rect.X; x < maxX; x++)
            {
                surf.SetGlyph(x, y, ' ', Color.Black, bg);
            }
        }
    }

    private static void RenderLaborSummaryColumn(ICellSurface surf, Rectangle area, HumanFortress.Simulation.World.World world, FortressRuntimeAccess? runtime)
    {
        var haulStats = runtime?.TransportJobs?.GetLastStatsSnapshot();
        var miningStats = runtime?.MiningJobs?.GetLastStatsSnapshot();
        var craftStats = runtime?.CraftJobs?.GetLastStatsSnapshot();
        int haulBacklog = haulStats?.Backlog ?? (runtime?.TransportJobs?.GetBacklogCount() ?? 0);
        int miningBacklog = miningStats?.Backlog ?? (runtime?.MiningJobs?.GetBacklogCount() ?? 0);
        int craftBacklog = craftStats?.Backlog ?? 0;
        int constructionSites = world.Orders.GetActiveConstructionSnapshot().Count;
        int totalDwarves = world.Creatures.GetAllInstances().Count(c => c.HP > 0);
        int busyWorkers = (haulStats?.Active ?? 0) + (miningStats?.Active ?? 0) + (craftStats?.Active ?? 0);
        int idleDwarves = System.Math.Max(0, totalDwarves - busyWorkers);

        var cards = new System.Collections.Generic.List<WorkCategoryCard>
        {
            new("Hauling", $"Backlog {haulBacklog}", haulStats?.Active ?? 0, haulBacklog, new Color(120, 180, 255)),
            new("Mining", $"Backlog {miningBacklog}", miningStats?.Active ?? 0, miningBacklog, new Color(200, 220, 120)),
            new("Construction", $"Sites {constructionSites}", runtime?.ConstructionJobs?.LastIntakeCount ?? 0, constructionSites, new Color(255, 200, 120)),
            new("Farming", "Crop planner coming soon", 0, 0, new Color(160, 210, 120)),
            new("Crafting", $"Backlog {craftBacklog}", craftStats?.Active ?? 0, craftBacklog, new Color(200, 160, 255)),
            new("Service", $"Idle dwarves {idleDwarves}", idleDwarves, 0, new Color(150, 200, 200))
        };

        int rowY = area.Y + 1;
        surf.Print(area.X + 1, rowY++, "[Labor Overview]", Color.Yellow);
        rowY++;
        foreach (var card in cards)
        {
            if (rowY + 1 >= area.Y + area.Height) break;
            Color bg = new Color(card.Accent.R / 8, card.Accent.G / 8, card.Accent.B / 8);
            for (int y = 0; y < 2; y++)
            {
                for (int x = area.X + 1; x < area.X + area.Width - 1; x++)
                {
                    surf.SetGlyph(x, rowY + y, ' ', Color.Black, bg);
                }
            }
            surf.Print(area.X + 2, rowY, card.Name, Color.White);
            surf.Print(area.X + 2, rowY + 1, card.Detail, Color.Gray);
            surf.Print(area.X + area.Width - 13, rowY, $"Act:{card.Active,3}", card.Accent);
            surf.Print(area.X + area.Width - 13, rowY + 1, $"Back:{card.Backlog,3}", Color.LightGray);
            rowY += 3;
        }

        surf.Print(area.X + 1, area.Y + area.Height - 2, $"Total dwarves: {totalDwarves}", Color.Cyan);
    }

    private static void RenderDwarfRosterColumn(ICellSurface surf, Rectangle area, HumanFortress.Simulation.World.World world, UiStore ui, FortressRuntimeAccess? runtime)
    {
        var roster = runtime?.GetProfessionRosterSnapshot()
            ?? Array.Empty<HumanFortress.App.Jobs.ProfessionAssignments.ProfessionRosterEntry>();
        var service = runtime?.ProfessionAssignments;
        var nameLookup = service?.Registry.Definitions.ToDictionary(d => d.Id, d => d.Name, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        surf.Print(area.X + 1, area.Y, "Dwarves On Duty", Color.Yellow);
        surf.Print(area.X + 1, area.Y + 1, "Name         Status", Color.Gray);

        if (roster.Count == 0)
        {
            surf.Print(area.X + 1, area.Y + 3, "No dwarves available.", Color.DarkGray);
            return;
        }

        int rowY = area.Y + 2;
        int maxRows = area.Height - 3;
        for (int i = 0; i < roster.Count && rowY < area.Y + area.Height - 2; i++)
        {
            var entry = roster[i];
            var creature = world.Creatures.GetInstance(entry.WorkerId);
            if (creature == null) continue;
            var def = world.Creatures.GetDefinition(creature.DefinitionId);
            string name = def?.Name ?? creature.DefinitionId;
            string status = creature.HP > 0 ? "OK" : "Injured";
            Color statusColor = creature.HP > 0 ? Color.Green : Color.Red;
            if (i == ui.WorkPanelSelectedIndex)
            {
                for (int x = area.X + 1; x < area.X + area.Width - 1; x++)
                    surf.SetGlyph(x, rowY, ' ', Color.Black, new Color(40, 40, 10));
            }
            surf.Print(area.X + 1, rowY, $"{Truncate(name, 18),-18}", Color.White);
            surf.Print(area.X + area.Width - 8, rowY, status, statusColor);
            rowY++;
        }
    }

    private static void RenderSchedulerColumn(ICellSurface surf, Rectangle area, ulong tick, string caption, FortressRuntimeAccess? runtime)
    {
        var sched = runtime?.JobsOrchestrator?.GetLastStats();
        var haulStats = runtime?.TransportJobs?.GetLastStatsSnapshot();
        var miningStats = runtime?.MiningJobs?.GetLastStatsSnapshot();
        var craftStats = runtime?.CraftJobs?.GetLastStatsSnapshot();

        int line = area.Y;
        surf.Print(area.X + 1, line++, caption, Color.Yellow);
        if (sched.HasValue)
        {
            var s = sched.Value;
            surf.Print(area.X + 1, line++, $"Plan: {s.PlanMsTotal} ms  Apply: {s.ApplyMsTotal} ms", Color.Cyan);
            surf.Print(area.X + 1, line++, $"Intake H:{s.IntakeHaul} M:{s.IntakeMining} C:{s.IntakeConstruction} Cr:{s.IntakeCraft}", Color.Gray);
            line++;
        }

        if (haulStats.HasValue)
        {
            var hs = haulStats.Value;
            surf.Print(area.X + 1, line++, $"[Haul] Active:{hs.Active} Backlog:{hs.Backlog}", Color.White);
            surf.Print(area.X + 1, line++, $"Carry:{hs.CarryoverOld} +Done:{hs.CompletedDelta} +Retry:{hs.RequeuedDelta}", Color.DarkGray);
        }

        if (miningStats.HasValue)
        {
            var ms = miningStats.Value;
            surf.Print(area.X + 1, line++, $"[Mine] Active:{ms.Active} Backlog:{ms.Backlog} Deferred:{ms.Deferred}", Color.White);
            surf.Print(area.X + 1, line++, $"Carry:{ms.CarryoverOld} Reserved:{ms.ReservedTiles}", Color.DarkGray);
        }

        if (craftStats.HasValue)
        {
            var cs = craftStats.Value;
            surf.Print(area.X + 1, line++, $"[Craft] Active:{cs.Active} Backlog:{cs.Backlog}", Color.White);
            surf.Print(area.X + 1, line++, $"Intake:{cs.Intake} +Done:{cs.CompletedDelta}", Color.DarkGray);
        }

        if (craftStats.HasValue)
        {
            var cs = craftStats.Value;
            surf.Print(area.X + 1, line++, $"[Craft] Active:{cs.Active} Backlog:{cs.Backlog}", Color.White);
            surf.Print(area.X + 1, line++, $"Intake:{cs.Intake} +Done:{cs.CompletedDelta}", Color.DarkGray);
        }

        var debug = runtime?.GetJobsDebugData(tick);
        if (debug.HasValue && debug.Value.Transport.HasValue)
        {
            var tdbg = debug.Value.Transport.Value;
            line++;
            surf.Print(area.X + 1, line++, "Queue peek:", Color.LightCyan);
            foreach (var req in tdbg.PendingPeek.Take(2))
            {
                string reason = req.Reason.ToString();
                if (string.IsNullOrWhiteSpace(reason)) reason = "Request";
                surf.Print(area.X + 2, line++, $"{reason} -> ({req.To.X},{req.To.Y},{req.ToZ})", Color.Gray);
            }

            if (tdbg.ShardCounts.Count > 0)
            {
                var shardLine = string.Join(" ", tdbg.ShardCounts.Take(3).Select(kv => $"{kv.Key}:{kv.Value}"));
                surf.Print(area.X + 1, line++, $"Shards: {shardLine}", Color.DarkGray);
            }
        }
    }

    private static void RenderWorkshopListColumn(ICellSurface surf, Rectangle area, HumanFortress.Simulation.World.World world)
    {
        var workshops = CollectWorkshops(world);
        surf.Print(area.X + 1, area.Y, "Workshops", Color.Yellow);
        if (workshops.Count == 0)
        {
            surf.Print(area.X + 1, area.Y + 2, "No workshops constructed yet.", Color.DarkGray);
            surf.Print(area.X + 1, area.Y + 4, "Build a Stoneworks to cut", Color.Gray);
            surf.Print(area.X + 1, area.Y + 5, "boulders into blocks.", Color.Gray);
            return;
        }

        int line = area.Y + 2;
        int maxLine = area.Y + area.Height - 2;
        foreach (var ws in workshops.OrderBy(w => w.Instance.Z).ThenBy(w => w.Instance.Position.Y).ThenBy(w => w.Instance.Position.X))
        {
            if (line >= maxLine) break;
            var state = ws.Instance.Workshop;
            string status = ws.IsSite ? "Site" : $"Workers {state?.ActiveJobs ?? 0}/{state?.AllowedWorkers ?? 0}";
            if (!ws.IsSite && state != null)
            {
                status += $"  Queue {state.Queue.Count}";
            }
            bool blocked = state != null && state.Queue.Any(e => e.Status == CraftQueueStatus.AwaitingMaterials);
            Color fg = ws.IsSite ? Color.DarkGray : (blocked ? Color.Orange : Color.White);
            surf.Print(area.X + 1, line++, $"{ws.Name}", fg);
            if (line >= maxLine) break;
            surf.Print(area.X + 3, line++, $"Pos ({ws.Instance.Position.X},{ws.Instance.Position.Y},{ws.Instance.Z}) [{status}]", Color.Gray);
        }

        if (workshops.Count > (maxLine - (area.Y + 2)) / 2)
        {
            surf.Print(area.X + 1, maxLine, $"... {workshops.Count - (maxLine - (area.Y + 2)) / 2} more", Color.DarkGray);
        }
    }

    private static void RenderWorkshopNotesColumn(ICellSurface surf, Rectangle area, HumanFortress.Simulation.World.World world)
    {
        var workshops = CollectWorkshops(world);
        surf.Print(area.X + 1, area.Y, "Active Queues", Color.Yellow);
        int line = area.Y + 2;
        if (workshops.Count == 0)
        {
            surf.Print(area.X + 1, line, "No workshops online.", Color.DarkGray);
            return;
        }

        foreach (var ws in workshops.OrderBy(w => w.Instance.Z))
        {
            if (line >= area.Y + area.Height - 2) break;
            var state = ws.Instance.Workshop;
            surf.Print(area.X + 1, line++, ws.Name, state?.Queue.Any(e => e.Status == CraftQueueStatus.AwaitingMaterials) == true ? Color.Orange : Color.White);
            if (state == null || state.Queue.Count == 0)
            {
                if (line >= area.Y + area.Height - 2) break;
                surf.Print(area.X + 2, line++, "No queued recipes.", Color.DarkGray);
                continue;
            }
            foreach (var entry in state.Queue)
            {
                if (line >= area.Y + area.Height - 2) break;
                string prefix = entry.Status switch
                {
                    CraftQueueStatus.InProgress => ">",
                    CraftQueueStatus.AwaitingMaterials => "!",
                    CraftQueueStatus.Scheduled => "*",
                    _ => "-"
                };
                string status = entry.Status switch
                {
                    CraftQueueStatus.InProgress => entry.ActiveWorkerId.HasValue ? $"Working ({entry.ActiveWorkerId.Value.ToString("N")[..6]})" : "Working",
                    CraftQueueStatus.AwaitingMaterials => entry.BlockingReason ?? "Waiting for inputs",
                    CraftQueueStatus.Scheduled => "Assigned",
                    _ => "Ready"
                };
                surf.Print(area.X + 2, line++, $"{prefix} {entry.DisplayName} - {status}", entry.Status == CraftQueueStatus.AwaitingMaterials ? Color.Orange : Color.Gray);
            }
        }
    }

    private static void RenderOrdersSummaryColumn(ICellSurface surf, Rectangle area, HumanFortress.Simulation.World.World world)
    {
        var haulOrders = world.Orders.GetActiveHaulsSnapshot();
        var miningOrders = world.Orders.GetActiveMiningSnapshot();
        var constructionOrders = world.Orders.GetActiveConstructionSnapshot();

        surf.Print(area.X + 1, area.Y, "Order Summary", Color.Yellow);
        int line = area.Y + 2;
        surf.Print(area.X + 1, line++, $"Haul designations: {haulOrders.Count}", Color.Cyan);
        surf.Print(area.X + 1, line++, $"Mining designations: {miningOrders.Count}", Color.Cyan);
        surf.Print(area.X + 1, line++, $"Construction sites: {constructionOrders.Count}", Color.Cyan);
        line++;
        surf.Print(area.X + 1, line++, "Hints:", Color.Yellow);
        surf.Print(area.X + 2, line++, "- Use Z menu to add jobs", Color.Gray);
        surf.Print(area.X + 2, line++, "- Shift-click cancels orders", Color.Gray);
    }

    private static void RenderJobsColumn(ICellSurface surf, Rectangle area, HumanFortress.Simulation.World.World world, FortressRuntimeAccess? runtime)
    {
        var rows = BuildActiveJobRows(runtime);
        surf.Print(area.X + 1, area.Y, "Active Jobs", Color.Yellow);
        surf.Print(area.X + 1, area.Y + 1, "Type  Worker   Stage          Target", Color.Gray);
        int line = area.Y + 2;
        int maxRows = area.Height - 6;
        foreach (var row in rows.Take(maxRows))
        {
            surf.Print(area.X + 1, line++, $"{row.Kind,-5}{row.Worker,-9}{Truncate(row.Stage, 14),-14}{row.Target}", row.Color);
        }

        if (rows.Count == 0)
        {
            surf.Print(area.X + 1, line++, "No active jobs. Use Orders menu to queue work.", Color.DarkGray);
        }

        line += 1;
        surf.Print(area.X + 1, line++, "Recent designations:", Color.Yellow);
        var recentHaul = world.Orders.GetRecentHauls().Take(2).ToList();
        foreach (var d in recentHaul)
        {
            surf.Print(area.X + 2, line++, $"[Haul] Rect ({d.WorldRect.X},{d.WorldRect.Y}) {d.WorldRect.Width}x{d.WorldRect.Height} z={d.Z}", Color.Cyan);
        }
        var recentMining = world.Orders.GetRecentMining().Take(2).ToList();
        foreach (var d in recentMining)
        {
            surf.Print(area.X + 2, line++, $"[Mine] Rect ({d.Rect.X},{d.Rect.Y}) {d.Rect.Width}x{d.Rect.Height} z={d.ZMin}->{d.ZMax}", Color.LightGreen);
        }
        if (recentHaul.Count == 0 && recentMining.Count == 0)
        {
            surf.Print(area.X + 2, line++, "(No recent orders)", Color.DarkGray);
        }
    }

    private static System.Collections.Generic.List<ActiveJobRow> BuildActiveJobRows(FortressRuntimeAccess? runtime)
    {
        var rows = new System.Collections.Generic.List<ActiveJobRow>();
        var haulJobs = runtime?.TransportJobs?.GetActiveJobsSnapshot();
        if (haulJobs != null)
        {
            foreach (var job in haulJobs)
            {
                var worker = job.CreatureId.ToString("N").Substring(0, 6);
                rows.Add(new ActiveJobRow("Haul", worker, job.Stage, $"{job.Dest.X},{job.Dest.Y},{job.Dest.Z}", Color.Cyan));
            }
        }
        var miningJobs = runtime?.MiningJobs?.GetActiveJobsSnapshot();
        if (miningJobs != null)
        {
            foreach (var job in miningJobs)
            {
                var worker = job.WorkerId.ToString("N").Substring(0, 6);
                rows.Add(new ActiveJobRow("Mine", worker, job.Stage, $"{job.Target.X},{job.Target.Y},{job.Z}", Color.LightGreen));
            }
        }
        var craftJobs = runtime?.CraftJobs?.GetActiveJobsSnapshot();
        if (craftJobs != null)
        {
            foreach (var job in craftJobs)
            {
                var worker = job.WorkerId.ToString("N").Substring(0, 6);
                rows.Add(new ActiveJobRow("Craft", worker, job.Stage, $"{job.RecipeId}", new Color(200, 160, 255)));
            }
        }
        return rows;
    }

    private static void RenderStandingOrdersColumn(ICellSurface surf, Rectangle area)
    {
        surf.Print(area.X + 1, area.Y, "Standing Orders", Color.Yellow);
        int line = area.Y + 2;
        var toggles = new (string Label, string Value)[]
        {
            ("Auto-haul refuse", "Enabled (placeholder)"),
            ("Auto-weave cloth", "Enabled (placeholder)"),
            ("Kitchen cooking", "Allow seeds (TODO)"),
            ("Stone use", "All stone (TODO)")
        };
        foreach (var toggle in toggles)
        {
            if (line >= area.Y + area.Height - 1) break;
            surf.Print(area.X + 1, line++, toggle.Label, Color.White);
            surf.Print(area.X + 3, line++, toggle.Value, Color.Gray);
        }
        surf.Print(area.X + 1, area.Y + area.Height - 2, "TODO: Wire to standing-order data", Color.DarkGray);
    }

    private static void RenderWorkshopDirectory(ICellSurface surf, Rectangle area, HumanFortress.Simulation.World.World world)
    {
        var workshops = CollectWorkshops(world);
        surf.Print(area.X + 1, area.Y, $"Workshops ({workshops.Count})", Color.Yellow);
        int line = area.Y + 2;
        foreach (var ws in workshops.Take(area.Height - 2))
        {
            string label = $"{ws.Name,-18} ({ws.Instance.Position.X,3},{ws.Instance.Position.Y,3},{ws.Instance.Z,2})";
            Color color = ws.IsSite ? Color.Orange : Color.White;
            surf.Print(area.X + 1, line++, label, color);
        }
        if (workshops.Count == 0)
        {
            surf.Print(area.X + 1, line, "No workshops placed yet.", Color.DarkGray);
        }
    }

    private static void RenderConstructionStatusColumn(ICellSurface surf, Rectangle area, HumanFortress.Simulation.World.World world, FortressRuntimeAccess? runtime)
    {
        var workshops = CollectWorkshops(world);
        int built = workshops.Count(w => !w.IsSite);
        int sites = workshops.Count(w => w.IsSite);

        surf.Print(area.X + 1, area.Y, "Construction Status", Color.Yellow);
        int line = area.Y + 2;
        surf.Print(area.X + 1, line++, $"Built workshops: {built}", Color.LightGreen);
        surf.Print(area.X + 1, line++, $"Sites in progress: {sites}", Color.Orange);
        surf.Print(area.X + 1, line++, $"Queued designations: {world.Orders.GetActiveBuildableSnapshot().Count}", Color.Cyan);
        surf.Print(area.X + 1, line++, $"Last tick processed: {runtime?.ConstructionJobs?.LastProcessedSites ?? 0}", Color.Gray);
        surf.Print(area.X + 1, line++, $"Intake limit: {runtime?.SchedulerTunings?.Construction.PlanPerTick ?? 0}", Color.DarkGray);
    }

    private sealed record WorkshopDisplay(string Name, HumanFortress.Simulation.Placeables.PlaceableInstance Instance, HumanFortress.Core.Content.Registry.ConstructionDefinition? Definition, bool IsSite);

    private static System.Collections.Generic.List<WorkshopDisplay> CollectWorkshops(HumanFortress.Simulation.World.World world)
    {
        var list = new System.Collections.Generic.List<WorkshopDisplay>();
        var registry = HumanFortress.Core.Content.Registry.ConstructionRegistry.Instance;
        foreach (var chunk in world.GetAllChunks())
        {
            var pd = chunk.GetPlaceableData();
            if (pd == null) continue;
            foreach (var p in pd.GetAllOwnedPlaceables())
            {
                string defId = p.ConstructionSite?.TargetId ?? p.DefinitionId;
                var def = registry.GetConstruction(defId);
                if (def == null) continue;
                bool isWorkshop = string.Equals(def.Category, "workshop", System.StringComparison.OrdinalIgnoreCase)
                                  || (def.PlaceableProfile.Tags != null && Array.IndexOf(def.PlaceableProfile.Tags, "workshop") >= 0);
                if (!isWorkshop) continue;
                list.Add(new WorkshopDisplay(def.Name, p, def, p.ConstructionSite != null));
            }
        }
        return list;
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
        return value.Substring(0, System.Math.Max(0, max - 1)) + "...";
    }

    public static void DrawWorkshopsOverlay(MapScreenSurface mapSurface, HumanFortress.Simulation.World.World world, int currentZ, SadRogue.Primitives.Rectangle viewport)
    {
        var reg = HumanFortress.Core.Content.Registry.ConstructionRegistry.Instance;
        var border = new Color(255, 230, 0);         // completed
        var fill = new Color(255, 230, 0, 90);
        var siteBorder = new Color(255, 140, 0);     // construction site
        var siteFill = new Color(255, 140, 0, 60);
        var surf = mapSurface.Surface;
        foreach (var chunk in world.GetAllChunks())
        {
            var pd = chunk.GetPlaceableData();
            if (pd == null) continue;
            foreach (var p in pd.GetAllOwnedPlaceables())
            {
                if (p.Z != currentZ) continue;
                bool isSite = p.ConstructionSite != null;
                string defId = isSite ? p.ConstructionSite!.TargetId : p.DefinitionId;
                var def = reg.GetConstruction(defId);
                if (def == null) continue;
                bool isWorkshop = string.Equals(def.Category, "workshop", StringComparison.OrdinalIgnoreCase)
                                  || (def.PlaceableProfile.Tags != null && Array.IndexOf(def.PlaceableProfile.Tags, "workshop") >= 0);
                if (!isWorkshop) continue;
                var fp = p.Footprint;
                var borderColor = isSite ? siteBorder : border;
                var fillColor = isSite ? siteFill : fill;
                // Fill footprint
                for (int dy = 0; dy < fp.D; dy++)
                {
                    for (int dx = 0; dx < fp.W; dx++)
                    {
                        int wx = p.Position.X + dx;
                        int wy = p.Position.Y + dy;
                        int sx = wx - viewport.X;
                        int sy = wy - viewport.Y;
                        if (sx >= 0 && sx < mapSurface.Width && sy >= 0 && sy < mapSurface.Height)
                        {
                            mapSurface.SetGlyph(sx, sy, '.', fillColor, Color.Transparent);
                        }
                    }
                }
                // Border
                for (int dx = 0; dx < fp.W; dx++)
                {
                    int sx = p.Position.X + dx - viewport.X;
                    int sy1 = p.Position.Y - viewport.Y;
                    int sy2 = p.Position.Y + fp.D - 1 - viewport.Y;
                    if (sx >= 0 && sx < mapSurface.Width)
                    {
                        if (sy1 >= 0 && sy1 < mapSurface.Height) mapSurface.SetGlyph(sx, sy1, '-', borderColor, Color.Transparent);
                        if (sy2 >= 0 && sy2 < mapSurface.Height) mapSurface.SetGlyph(sx, sy2, '-', borderColor, Color.Transparent);
                    }
                }
                for (int dy = 0; dy < fp.D; dy++)
                {
                    int sy = p.Position.Y + dy - viewport.Y;
                    int sx1 = p.Position.X - viewport.X;
                    int sx2 = p.Position.X + fp.W - 1 - viewport.X;
                    if (sy >= 0 && sy < mapSurface.Height)
                    {
                        if (sx1 >= 0 && sx1 < mapSurface.Width) mapSurface.SetGlyph(sx1, sy, '|', borderColor, Color.Transparent);
                        if (sx2 >= 0 && sx2 < mapSurface.Width) mapSurface.SetGlyph(sx2, sy, '|', borderColor, Color.Transparent);
                    }
                }

                // Materials progress text (site only): e.g., "B 6/8 · P 2/4" at footprint top-left
                if (isSite && p.ConstructionSite != null)
                {
                    var delivered = CountDeliveredOnFootprintOrRing(world, p);
                    var req = p.ConstructionSite.MaterialsRequired;
                    int bD = delivered.TryGetValue("block", out var bd) ? bd : 0;
                    int pD = delivered.TryGetValue("plank", out var pdv) ? pdv : 0;
                    int bR = req.TryGetValue("block", out var br) ? br : 0;
                    int pR = req.TryGetValue("plank", out var pr) ? pr : 0;
                    string text = $"B {bD}/{bR} · P {pD}/{pR}";
                    int tx = p.Position.X - viewport.X;
                    int ty = p.Position.Y - viewport.Y - 1; // one row above top edge if possible
                    if (ty < 0) ty = p.Position.Y - viewport.Y; // fallback to inside
                    if (tx >= 0 && ty >= 0 && tx + text.Length < surf.Width && ty < surf.Height)
                    {
                        surf.Print(tx, ty, text, Color.White);
                    }
                }
            }
        }
    }

    // Snapshot-driven overlay rendering (optional path)
    public static void DrawWorkshopsOverlayFromSnapshot(MapScreenSurface mapSurface, HumanFortress.Simulation.Rendering.RenderSnapshot snapshot, int currentZ, SadRogue.Primitives.Rectangle viewport)
    {
        var border = new Color(255, 230, 0);         // completed
        var fill = new Color(255, 230, 0, 90);
        var siteBorder = new Color(255, 140, 0);     // construction site
        var siteFill = new Color(255, 140, 0, 60);
        var surf = mapSurface.Surface;
        foreach (var ch in snapshot.Chunks)
        {
            foreach (var zslice in ch.ZSlices)
            {
                if (zslice.ZIndex != currentZ) continue;
                foreach (var rect in zslice.PlaceablesOverlay)
                {
                    bool isSite = string.Equals(rect.Kind, "workshop_site", System.StringComparison.OrdinalIgnoreCase);
                    var borderColor = isSite ? siteBorder : border;
                    var fillColor = isSite ? siteFill : fill;

                    // Fill footprint
                    for (int dy = 0; dy < rect.H; dy++)
                    {
                        for (int dx = 0; dx < rect.W; dx++)
                        {
                            int wx = rect.X + dx;
                            int wy = rect.Y + dy;
                            int sx = wx - viewport.X;
                            int sy = wy - viewport.Y;
                            if (sx >= 0 && sx < mapSurface.Width && sy >= 0 && sy < mapSurface.Height)
                            {
                                mapSurface.SetGlyph(sx, sy, '.', fillColor, Color.Transparent);
                            }
                        }
                    }
                    // Border
                    for (int dx = 0; dx < rect.W; dx++)
                    {
                        int sx = rect.X + dx - viewport.X;
                        int sy1 = rect.Y - viewport.Y;
                        int sy2 = rect.Y + rect.H - 1 - viewport.Y;
                        if (sx >= 0 && sx < mapSurface.Width)
                        {
                            if (sy1 >= 0 && sy1 < mapSurface.Height) mapSurface.SetGlyph(sx, sy1, '-', borderColor, Color.Transparent);
                            if (sy2 >= 0 && sy2 < mapSurface.Height) mapSurface.SetGlyph(sx, sy2, '-', borderColor, Color.Transparent);
                        }
                    }
                    for (int dy = 0; dy < rect.H; dy++)
                    {
                        int sy = rect.Y + dy - viewport.Y;
                        int sx1 = rect.X - viewport.X;
                        int sx2 = rect.X + rect.W - 1 - viewport.X;
                        if (sy >= 0 && sy < mapSurface.Height)
                        {
                            if (sx1 >= 0 && sx1 < mapSurface.Width) mapSurface.SetGlyph(sx1, sy, '|', borderColor, Color.Transparent);
                            if (sx2 >= 0 && sx2 < mapSurface.Width) mapSurface.SetGlyph(sx2, sy, '|', borderColor, Color.Transparent);
                        }
                    }
                }
            }
        }
    }

    private static System.Collections.Generic.Dictionary<string, int> CountDeliveredOnFootprintOrRing(HumanFortress.Simulation.World.World world, HumanFortress.Simulation.Placeables.PlaceableInstance site)
    {
        var delivered = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        var fp = site.Footprint;
        var seen = new System.Collections.Generic.HashSet<(int,int)>();
        // footprint
        for (int dy = 0; dy < fp.D; dy++)
        for (int dx = 0; dx < fp.W; dx++)
        {
            int wx = site.Position.X + dx;
            int wy = site.Position.Y + dy;
            if (seen.Add((wx, wy)))
                AddDeliveredAt(world, site.Z, wx, wy, site, delivered);
        }
        // ring (4-neighbor)
        var dirs = new (int dx,int dy)[] { (1,0), (-1,0), (0,1), (0,-1) };
        for (int dy = 0; dy < fp.D; dy++)
        for (int dx = 0; dx < fp.W; dx++)
        {
            int wx = site.Position.X + dx;
            int wy = site.Position.Y + dy;
            foreach (var (adx, ady) in dirs)
            {
                int nx = wx + adx;
                int ny = wy + ady;
                if (!world.IsValidPosition(nx, ny, site.Z)) continue;
                if (seen.Add((nx, ny)))
                    AddDeliveredAt(world, site.Z, nx, ny, site, delivered);
            }
        }
        return delivered;
    }

    private static void AddDeliveredAt(HumanFortress.Simulation.World.World world, int z, int x, int y, HumanFortress.Simulation.Placeables.PlaceableInstance site, System.Collections.Generic.Dictionary<string, int> delivered)
    {
        foreach (var it in world.Items.GetAllInstances())
        {
            if (it.IsCarried) continue;
            if (it.Position.X != x || it.Position.Y != y || it.Z != z) continue;
            var def = world.Items.GetDefinition(it.DefinitionId);
            if (def == null || def.Tags == null) continue;
            foreach (var req in site.ConstructionSite!.MaterialsRequired.Keys)
            {
                if (MatchesRequirement(def.Tags, req))
                {
                    delivered[req] = delivered.GetValueOrDefault(req, 0) + it.StackCount;
                    break;
                }
            }
        }
    }

    private static bool MatchesRequirement(System.Collections.Generic.IEnumerable<string> itemTags, string requirement)
    {
        var set = new System.Collections.Generic.HashSet<string>(itemTags, System.StringComparer.OrdinalIgnoreCase);
        switch (requirement.ToLowerInvariant())
        {
            case "block":
                return set.Contains("block") || set.Contains("stone_block") || set.Contains("brick") || (set.Contains("stone") && set.Contains("block"));
            case "plank":
                return set.Contains("plank") || set.Contains("wood_plank") || (set.Contains("wood") && set.Contains("plank"));
            case "stone_block":
                return set.Contains("stone") && set.Contains("block");
            case "wood_plank":
                return set.Contains("wood") && set.Contains("plank");
            case "wood_log":
                return set.Contains("wood") && set.Contains("log");
            default:
                return set.Contains(requirement);
        }
    }

    public static void DrawWorkshopPanel(ScreenSurface surface, UiStore ui, HumanFortress.Simulation.World.World world, ulong tick)
    {
        if (!ui.WorkshopPanelOpen || ui.OpenWorkshopGuid == null) return;

        // Find placeable by GUID
        HumanFortress.Simulation.Placeables.PlaceableInstance? found = null;
        foreach (var ch in world.GetAllChunks())
        {
            var pd = ch.GetPlaceableData();
            if (pd == null) continue;
            foreach (var p in pd.GetAllOwnedPlaceables())
            {
                if (p.Guid == ui.OpenWorkshopGuid.Value) { found = p; break; }
            }
            if (found != null) break;
        }
        if (found == null) { return; }

        var reg = HumanFortress.Core.Content.Registry.ConstructionRegistry.Instance;
        var def = reg.GetConstruction(found.DefinitionId);
        string title = def?.Name ?? found.DefinitionId;
        var fp = found.Footprint;
        var state = found.Workshop ?? new HumanFortress.Simulation.Placeables.WorkshopState();

        // Panel geometry (centered)
        var surf = surface.Surface;
        int w = 56, h = 16;
        int x0 = (surf.Width - w) / 2;
        int y0 = (surf.Height - h) / 2;
        var bg = Color.Black.SetAlpha(220);
        var fg = Color.White;
        var hi = Color.Cyan;
        for (int y = y0; y < y0 + h; y++)
        for (int x = x0; x < x0 + w; x++)
            surf.SetGlyph(x, y, ' ', fg, bg);
        for (int x = x0; x < x0 + w; x++)
        {
            surf.SetGlyph(x, y0, '-');
            surf.SetGlyph(x, y0 + h - 1, '-');
        }
        for (int y = y0; y < y0 + h; y++)
        {
            surf.SetGlyph(x0, y, '|');
            surf.SetGlyph(x0 + w - 1, y, '|');
        }
        surf.SetGlyph(x0, y0, '+'); surf.SetGlyph(x0 + w - 1, y0, '+');
        surf.SetGlyph(x0, y0 + h - 1, '+'); surf.SetGlyph(x0 + w - 1, y0 + h - 1, '+');

        // Header and basics
        surf.Print(x0 + 2, y0, $" {title} ", hi);
        surf.Print(x0 + 2, y0 + 2, $"Id: {found.DefinitionId}", fg);
        surf.Print(x0 + 2, y0 + 3, $"Pos: ({found.Position.X},{found.Position.Y},{found.Z})", fg);
        surf.Print(x0 + 2, y0 + 4, $"Footprint: {fp.W}x{fp.D}  Pass: {def?.PlaceableProfile.Passability}", fg);
        var tags = def?.PlaceableProfile.Tags ?? Array.Empty<string>();
        surf.Print(x0 + 2, y0 + 5, $"Tags: [{string.Join(',', tags)}]", Color.Gray);

        // Workers and automation
        surf.Print(x0 + 2, y0 + 7, $"Workers {state.ActiveJobs}/{state.AllowedWorkers} (Max {state.MaxWorkers})  [+]/[-]", Color.White);
        surf.Print(x0 + 2, y0 + 8, $"Supply: {(state.AutoRequestMaterials ? "Auto" : "Manual")} (S)   Stockpile: {(state.AutoStockpileOutputs ? "Auto" : "Manual")} (O)", Color.Gray);
        int attachCount = def?.AttachmentSlots?.Length ?? 0;
        surf.Print(x0 + 2, y0 + 9, $"Attachment slots: {attachCount}", Color.DarkGray);

        // Recipe queue
        surf.Print(x0 + 2, y0 + 11, "Queue [A:Add, Delete=Remove, PgUp/PgDn=Move]", Color.Yellow);
        int queueStart = y0 + 12;
        int maxRows = h - 4;
        int selected = Math.Clamp(ui.WorkshopQueueSelectedIndex, 0, Math.Max(0, state.Queue.Count - 1));
        if (state.Queue.Count == 0)
        {
            surf.Print(x0 + 2, queueStart, "Queue empty. Press A to add the default recipe.", Color.DarkGray);
        }
        else
        {
            int row = 0;
            foreach (var entry in state.Queue)
            {
                if (row >= maxRows) break;
                int y = queueStart + row;
                bool isSelected = row == selected;
                if (isSelected)
                {
                    for (int cx = x0 + 1; cx < x0 + w - 1; cx++)
                        surf.SetGlyph(cx, y, ' ', Color.White, new Color(30, 30, 10));
                }
                string status = entry.Status switch
                {
                    CraftQueueStatus.InProgress => entry.ActiveWorkerId.HasValue ? $"Working [{entry.ActiveWorkerId.Value.ToString("N")[..6]}]" : "Working",
                    CraftQueueStatus.AwaitingMaterials => entry.BlockingReason ?? "Awaiting inputs",
                    CraftQueueStatus.Scheduled => "Scheduled",
                    _ => "Ready"
                };
                char prefix = entry.Status switch
                {
                    CraftQueueStatus.InProgress => '>',
                    CraftQueueStatus.AwaitingMaterials => '!',
                    CraftQueueStatus.Scheduled => '*',
                    _ => '-'
                };
                var color = entry.Status == CraftQueueStatus.AwaitingMaterials ? Color.Orange : Color.White;
                surf.Print(x0 + 2, y, $"{prefix} {entry.DisplayName} - {status}", color);
                row++;
            }
        }

        // Footer
        surf.Print(x0 + 2, y0 + h - 2, "ESC/Right-click: close", Color.DarkGray);
        surf.Print(x0 + w - 18, y0 + h - 2, $"#{found.Guid.ToString()[..8]}", Color.DarkGray);
    }

    // Simple workshop footprint preview: draws a gold rectangle at the anchor with given footprint
    public static void DrawWorkshopPlacementPreview(ScreenSurface mapSurface, SadRogue.Primitives.Point anchor, HumanFortress.Core.Content.Registry.Footprint footprint, SadRogue.Primitives.Rectangle viewport, HumanFortress.Simulation.World.World? world)
    {
        var gold = new Color(255, 230, 0);
        for (int dy = 0; dy < footprint.D; dy++)
        {
            for (int dx = 0; dx < footprint.W; dx++)
            {
                int x = anchor.X + dx;
                int y = anchor.Y + dy;
                int sx = x - viewport.X;
                int sy = y - viewport.Y;
                if (sx >= 0 && sy >= 0 && sx < mapSurface.Width && sy < mapSurface.Height)
                {
                    mapSurface.SetGlyph(sx, sy, '.', gold, Color.Transparent);
                }
            }
        }
        // Outline
        for (int dx = 0; dx < footprint.W; dx++)
        {
            int sx = anchor.X + dx - viewport.X;
            int sy1 = anchor.Y - viewport.Y;
            int sy2 = anchor.Y + footprint.D - 1 - viewport.Y;
            if (sx >= 0 && sx < mapSurface.Width)
            {
                if (sy1 >= 0 && sy1 < mapSurface.Height) mapSurface.SetGlyph(sx, sy1, '-', gold, Color.Transparent);
                if (sy2 >= 0 && sy2 < mapSurface.Height) mapSurface.SetGlyph(sx, sy2, '-', gold, Color.Transparent);
            }
        }
        for (int dy = 0; dy < footprint.D; dy++)
        {
            int sy = anchor.Y + dy - viewport.Y;
            int sx1 = anchor.X - viewport.X;
            int sx2 = anchor.X + footprint.W - 1 - viewport.X;
            if (sy >= 0 && sy < mapSurface.Height)
            {
                if (sx1 >= 0 && sx1 < mapSurface.Width) mapSurface.SetGlyph(sx1, sy, '|', gold, Color.Transparent);
                if (sx2 >= 0 && sx2 < mapSurface.Width) mapSurface.SetGlyph(sx2, sy, '|', gold, Color.Transparent);
            }
        }
    }

    public static void DrawMiningJobHighlights(ScreenSurface mapSurface, HumanFortress.App.Jobs.MiningJobSystem? jobs, SadRogue.Primitives.Point camera, int currentZ, ulong tick)
    {
        if (jobs == null) return;
        var surf = mapSurface.Surface;
        bool flash = ((tick / 8) % 2) == 0;
        var fg = flash ? Color.Cyan : Color.DarkCyan;
        var bg = Color.Transparent;
        foreach (var j in jobs.GetActiveJobsSnapshot())
        {
            if (j.Z != currentZ) continue;
            int sx = j.Target.X - camera.X;
            int sy = j.Target.Y - camera.Y;
            if (sx >= 0 && sx < surf.Width && sy >= 0 && sy < surf.Height)
            {
                // Draw with transparent background to avoid obscuring underlying terrain
                surf.SetGlyph(sx, sy, '.', fg, Color.Transparent);
            }
        }
    }

    public static void DrawMiningCompletedHighlights(ScreenSurface mapSurface, HumanFortress.App.Jobs.MiningJobSystem? jobs, SadRogue.Primitives.Point camera, int currentZ, ulong tick)
    {
        if (jobs == null) return;
        var surf = mapSurface.Surface;
        var fg = new Color(255, 230, 0);
        var bg = Color.Transparent;
        foreach (var (cell, z) in jobs.GetRecentCompletions(tick))
        {
            if (z != currentZ) continue;
            int sx = cell.X - camera.X;
            int sy = cell.Y - camera.Y;
            if (sx >= 0 && sx < surf.Width && sy >= 0 && sy < surf.Height)
            {
                // Draw with transparent background to avoid obscuring underlying terrain
                surf.SetGlyph(sx, sy, '.', fg, Color.Transparent);
            }
        }
    }

    public static void DrawOrderHighlights(ScreenSurface mapSurface, UiStore ui, SadRogue.Primitives.Point camera, int currentZ, ulong tick, HumanFortress.Simulation.World.World? world)
    {
        var surf = mapSurface.Surface;
        var highlights = ui.GetHighlights();
        if (highlights.Count == 0) return;
        bool flash = ((tick / 10) % 2) == 0;
        var color = flash ? Color.Yellow : Color.Orange;
        foreach (var h in highlights)
        {
            if (currentZ < h.ZMin || currentZ > h.ZMax) continue;
            bool isMining = h.Kind.StartsWith("mining", System.StringComparison.OrdinalIgnoreCase);
            int x0 = h.Rect.X - camera.X;
            int y0 = h.Rect.Y - camera.Y;
            int x1 = x0 + h.Rect.Width - 1;
            int y1 = y0 + h.Rect.Height - 1;
            // Draw rectangle border unless this is a mining highlight; mining uses dot-only fill for eligibility
            if (!isMining)
            {
                var fg = flash ? Color.Yellow : Color.Orange;
                for (int x = x0; x <= x1; x++)
                {
                    if (x >= 0 && x < surf.Width)
                    {
                        if (y0 >= 0 && y0 < surf.Height) { surf.SetGlyph(x, y0, '-', fg, Color.Transparent); }
                        if (y1 >= 0 && y1 < surf.Height) { surf.SetGlyph(x, y1, '-', fg, Color.Transparent); }
                    }
                }
                for (int y = y0; y <= y1; y++)
                {
                    if (y >= 0 && y < surf.Height)
                    {
                        if (x0 >= 0 && x0 < surf.Width) { surf.SetGlyph(x0, y, '|', fg, Color.Transparent); }
                        if (x1 >= 0 && x1 < surf.Width) { surf.SetGlyph(x1, y, '|', fg, Color.Transparent); }
                    }
                }
                if (x0 >= 0 && x0 < surf.Width && y0 >= 0 && y0 < surf.Height) { surf.SetGlyph(x0, y0, '+', fg, Color.Transparent); }
                if (x1 >= 0 && x1 < surf.Width && y0 >= 0 && y0 < surf.Height) { surf.SetGlyph(x1, y0, '+', fg, Color.Transparent); }
                if (x0 >= 0 && x0 < surf.Width && y1 >= 0 && y1 < surf.Height) { surf.SetGlyph(x0, y1, '+', fg, Color.Transparent); }
                if (x1 >= 0 && x1 < surf.Width && y1 >= 0 && y1 < surf.Height) { surf.SetGlyph(x1, y1, '+', fg, Color.Transparent); }
            }

            // 不再整体遮罩矩形，避免遮挡原 tile，可视仅保留边框 + 合法格浅填

            // If mining highlight and world provided, shade only actually affected cells
            // We support encoded kind pattern: "mining:<Action>" (e.g., mining:DigChannel)
            if (world != null && isMining)
            {
                string action = "";
                int idx = h.Kind.IndexOf(':');
                if (idx >= 0 && idx + 1 < h.Kind.Length) action = h.Kind.Substring(idx + 1);
                // No fill block; instead draw a small center dot per legal tile with transparent background
                var dotFg = new Color(255, 230, 0);
                var dotBg = Color.Transparent;
                bool IsFill(HumanFortress.Simulation.Tiles.TerrainKind k)
                {
                    switch (action)
                    {
                        case nameof(MiningAction.DigRamp):
                            return k == HumanFortress.Simulation.Tiles.TerrainKind.SolidWall;
                        case nameof(MiningAction.DigChannel):
                            return k == HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor;
                        case nameof(MiningAction.DigStairwell):
                            // Only show fill on top layer to indicate starting floor
                            return currentZ == h.ZMin && k == HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor;
                        default:
                            // Dig and others: walls and ramps
                            return k == HumanFortress.Simulation.Tiles.TerrainKind.SolidWall || k == HumanFortress.Simulation.Tiles.TerrainKind.Ramp;
                    }
                }

                for (int wx = h.Rect.X; wx < h.Rect.X + h.Rect.Width; wx++)
                {
                    for (int wy = h.Rect.Y; wy < h.Rect.Y + h.Rect.Height; wy++)
                    {
                        var toptile = world.GetTile(wx, wy, currentZ);
                        if (toptile == null) continue;
                        if (!IsFill(toptile.Value.Kind)) continue;
                        int sx = wx - camera.X;
                        int sy = wy - camera.Y;
                        if (sx>=0 && sx<surf.Width && sy>=0 && sy<surf.Height)
                        {
                            surf.SetGlyph(sx, sy, '·', dotFg, Color.Transparent);
                        }
                    }
                }

                // Optional: stairwell top/bottom markers for z-min/z-max
                if (action == nameof(MiningAction.DigStairwell))
                {
                    if (currentZ == h.ZMin)
                    {
                        int sx = x0 + 1, sy = y0 + 1;
                        if (sx>=0 && sx<surf.Width && sy>=0 && sy<surf.Height)
                            surf.Print(sx, sy, "Top", Color.Cyan);
                    }
                    else if (currentZ == h.ZMax)
                    {
                        int sx = x0 + 1, sy = y0 + 1;
                        if (sx>=0 && sx<surf.Width && sy>=0 && sy<surf.Height)
                            surf.Print(sx, sy, "Bottom", Color.Cyan);
                    }
                }
            }

            // Construction highlight: draw gold dots on legal cells within rect
            if (world != null && h.Kind.StartsWith("construction", System.StringComparison.OrdinalIgnoreCase))
            {
                string shape = "";
                int idx = h.Kind.IndexOf(':');
                if (idx >= 0 && idx + 1 < h.Kind.Length) shape = h.Kind.Substring(idx + 1);
                var dotFg = new Color(255, 230, 0);
                for (int wx = h.Rect.X; wx < h.Rect.X + h.Rect.Width; wx++)
                {
                    for (int wy = h.Rect.Y; wy < h.Rect.Y + h.Rect.Height; wy++)
                    {
                        if (!IsConstructionCandidate(world, shape, wx, wy, currentZ)) continue;
                        int sx = wx - camera.X;
                        int sy = wy - camera.Y;
                        if (sx>=0 && sx<surf.Width && sy>=0 && sy<surf.Height)
                        {
                            surf.SetGlyph(sx, sy, '.', dotFg, Color.Transparent);
                        }
                    }
                }
            }
        }
    }

    private static void DrawWorkshopItemsPane(ScreenSurface surface, int x, int y, string category)
    {
        var bg = Color.Black.SetAlpha(200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        // Draw box background and border (local implementation)
        for (int i = 0; i < 38; i++)
            for (int j = 0; j < 12; j++)
                surface.SetGlyph(x + i, y + j, ' ', fg, bg);
        for (int i = 1; i < 38 - 1; i++)
        {
            surface.SetGlyph(x + i, y, '-');
            surface.SetGlyph(x + i, y + 12 - 1, '-');
        }
        for (int j = 1; j < 12 - 1; j++)
        {
            surface.SetGlyph(x, y + j, '|');
            surface.SetGlyph(x + 38 - 1, y + j, '|');
        }
        surface.SetGlyph(x, y, '+');
        surface.SetGlyph(x + 38 - 1, y, '+');
        surface.SetGlyph(x, y + 12 - 1, '+');
        surface.SetGlyph(x + 38 - 1, y + 12 - 1, '+');
        surface.Print(x + 1, y, $" {char.ToUpper(category[0]) + category.Substring(1)} ", highlight);

        var list = GetWorkshopsByCategory(category);
        var keys = new[] { 'Z','X','C','V','F','G','R','T' };
        int max = System.Math.Min(keys.Length, list.Count);
        for (int i = 0; i < max; i++)
        {
            var d = list[i];
            var fp = d.PlaceableProfile.Footprint;
            string size = $"{fp.W}x{fp.D}";
            surface.Print(x + 2, y + 2 + i, $"[{keys[i]}] {d.Name} ({size})", fg);
        }
        if (max == 0)
        {
            surface.Print(x + 2, y + 2, "WIP", Color.Gray);
        }
        surface.Print(x + 2, y + 10, "[,] Back", Color.Gray);
    }

    private static System.Collections.Generic.List<HumanFortress.Core.Content.Registry.ConstructionDefinition> GetWorkshopsByCategory(string category)
    {
        return WorkshopCategoryMapper.GetWorkshopsByCategory(category);
    }

    private static bool IsConstructionCandidate(HumanFortress.Simulation.World.World world, string shape, int x, int y, int z)
    {
        var t = world.GetTile(x, y, z);
        if (t == null) return false;
        var kind = t.Value.Kind;
        if (string.Equals(shape, nameof(HumanFortress.Simulation.Orders.ConstructionShape.Wall), System.StringComparison.OrdinalIgnoreCase))
        {
            return kind != HumanFortress.Simulation.Tiles.TerrainKind.SolidWall;
        }
        if (string.Equals(shape, nameof(HumanFortress.Simulation.Orders.ConstructionShape.Floor), System.StringComparison.OrdinalIgnoreCase))
        {
            if (kind == HumanFortress.Simulation.Tiles.TerrainKind.OpenWithFloor) return false;
            var below = world.GetTile(x, y, z - 1);
            if (below == null) return false;
            return below.Value.ProvidesSupport;
        }
        if (string.Equals(shape, nameof(HumanFortress.Simulation.Orders.ConstructionShape.Ramp), System.StringComparison.OrdinalIgnoreCase))
        {
            var top = world.GetTile(x, y, z + 1);
            if (top == null || top.Value.Kind != HumanFortress.Simulation.Tiles.TerrainKind.OpenNoFloor) return false;
            // any neighbor at z+1 standable
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx==0 && dy==0) continue;
                    var tn = world.GetTile(x+dx, y+dy, z+1);
                    if (tn != null && tn.Value.IsStandable) return true;
                }
            return false;
        }
        return false;
    }

    public static void DrawHelp(ScreenSurface mapSurface, UiStore ui)
    {
        if (!ui.HelpOpen) return;
        var surf = mapSurface.Surface;
        int width = Math.Min(50, surf.Width - 4);
        int height = Math.Min(14, surf.Height - 4);
        int x0 = (surf.Width - width) / 2;
        int y0 = (surf.Height - height) / 2;
        for (int yy = y0; yy < y0 + height; yy++)
            for (int x = x0; x < x0 + width; x++)
                surf.SetGlyph(x, yy, ' ', Color.White, new Color(10, 10, 10));

        string[] lines = new[]
        {
            "Bindings (default):",
            "F1..F7: Open panels | Z/X/C: Quick menus",
            "F9: Cycle overlay",
            "ESC: Back/Close | Right-Click: Cancel",
            "WASD: Move camera  | Q/E: Change Z",
            "Mouse: Move cursor  | Ctrl+Wheel: Zoom",
            "",
            "See docs/CONTROLS.md for full reference.",
        };
        int y = y0 + 1;
        foreach (var line in lines)
        {
            surf.Print(x0 + 2, y++, line, Color.White);
        }
    }

    public static void DrawToasts(ScreenSurface mapSurface, UiStore ui, ulong tick)
    {
        ui.PruneToasts(tick);
        var surf = mapSurface.Surface;
        int y = 1;
        foreach (var (text, _) in ui.Toasts)
        {
            surf.Print(2, y++, text, Color.Orange);
            if (y > 6) break;
        }
    }

    public static void DrawPause(ScreenSurface mapSurface, UiStore ui)
    {
        if (!ui.PauseOpen) return;
        var surf = mapSurface.Surface;
        int w = 26, h = 5;
        int x0 = (surf.Width - w) / 2;
        int y0 = (surf.Height - h) / 2;
        for (int yy = y0; yy < y0 + h; yy++)
            for (int x = x0; x < x0 + w; x++)
                surf.SetGlyph(x, yy, ' ', Color.White, new Color(20,20,20));
        surf.Print(x0 + 2, y0 + 1, "== PAUSED ==", Color.Yellow);
        surf.Print(x0 + 2, y0 + 3, "ESC resume | M main menu", Color.White);
    }

    public static void DrawDebug(ScreenSurface mapSurface, UiStore ui, SadRogue.Primitives.Point cursor, int currentZ, int zoomLevel, SadRogue.Primitives.Point camera, int fortressSize, HumanFortress.Simulation.World.World? world = null)
    {
        if (!ui.DebugOpen) return;
        var surf = mapSurface.Surface;
        // Size ~70% of screen, centered
        int width = Math.Min((int)(surf.Width * 0.7), surf.Width - 4);
        int height = Math.Min((int)(surf.Height * 0.6), surf.Height - 4);
        int x0 = (surf.Width - width) / 2;
        int y0 = (surf.Height - height) / 2;
        var bg = new Color(15, 15, 15, 180); // semi-transparent background

        // Fill background
        for (int yy = y0; yy < y0 + height; yy++)
            for (int x = x0; x < x0 + width; x++)
                surf.SetGlyph(x, yy, ' ', Color.White, bg);

        // Border
        for (int x = x0; x < x0 + width; x++)
        {
            surf.SetGlyph(x, y0, '-');
            surf.SetGlyph(x, y0 + height - 1, '-');
        }
        for (int y = y0; y < y0 + height; y++)
        {
            surf.SetGlyph(x0, y, '|');
            surf.SetGlyph(x0 + width - 1, y, '|');
        }
        surf.SetGlyph(x0, y0, '+');
        surf.SetGlyph(x0 + width - 1, y0, '+');
        surf.SetGlyph(x0, y0 + height - 1, '+');
        surf.SetGlyph(x0 + width - 1, y0 + height - 1, '+');

        // Title & Tabs (mouse clickable; Tab 键也可切换)
        surf.Print(x0 + 2, y0, "DEBUG MENU", Color.Cyan);
        int tabX = x0 + 22;
        var tabColor0 = ui.DebugMenuTab == 0 ? Color.Yellow : Color.DarkGray;
        var tabColor1 = ui.DebugMenuTab == 1 ? Color.Yellow : Color.DarkGray;
        var tabColor2 = ui.DebugMenuTab == 2 ? Color.Yellow : Color.DarkGray;
        surf.Print(tabX, y0, "Status", tabColor0);
        surf.Print(tabX + 8, y0, "|", Color.Gray);
        surf.Print(tabX + 10, y0, "Creatures", tabColor1);
        surf.Print(tabX + 20, y0, "|", Color.Gray);
        surf.Print(tabX + 22, y0, "Items", tabColor2);

        // Content based on tab
        if (ui.DebugMenuTab == 0) // Status tab
        {
            surf.Print(x0 + 2, y0 + 2, "=== Fortress Status ===", Color.Yellow);
            // World metrics (safe when world is null)
            int line = y0 + 4;
            if (world != null)
            {
                var chunksLoaded = world.GetAllChunks().Count();
                var itemsCount = world.Items.InstanceCount;
                var itemDefs = world.Items.DefinitionCount;
                var creatureDefs = world.Creatures.DefinitionCount;
                var creatures = world.Creatures.GetAllInstances().Count();
                surf.Print(x0 + 2, line++, $"Chunks: {chunksLoaded} / {fortressSize * fortressSize}", Color.Green);
                surf.Print(x0 + 2, line++, $"Items: {itemsCount} (defs {itemDefs})", Color.Green);
                surf.Print(x0 + 2, line++, $"Creatures: {creatures} (defs {creatureDefs})", Color.Green);
            }
            else
            {
                surf.Print(x0 + 2, line++, "World: N/A", Color.DarkGray);
                surf.Print(x0 + 2, line++, "Items: N/A", Color.DarkGray);
                surf.Print(x0 + 2, line++, "Creatures: N/A", Color.DarkGray);
            }
            surf.Print(x0 + 2, line++, $"Cursor: {cursor.X},{cursor.Y}", Color.White);
            surf.Print(x0 + 2, line++, $"Z-Level: {currentZ}", Color.Cyan);
            surf.Print(x0 + 2, line++, $"Zoom: {zoomLevel}x", Color.White);
            surf.Print(x0 + 2, line++, $"Camera: {camera.X},{camera.Y}", Color.Gray);
            surf.Print(x0 + 2, line++, $"Map: {fortressSize}x{fortressSize} chunks", Color.Gray);
        }
        else if (ui.DebugMenuTab == 1) // Creatures tab
        {
            surf.Print(x0 + 2, y0 + 2, "Spawn Creature:", Color.Yellow);
            int cx = x0 + 2; int cy = y0 + 3;
            WritePill(surf, ref cx, cy, "Dwarf", ui.DebugSelectedCreature.Contains("dwarf") ? Color.Black : Color.White, ui.DebugSelectedCreature.Contains("dwarf") ? Color.Yellow : new Color(40,40,40));
            WritePill(surf, ref cx, cy, "Human", ui.DebugSelectedCreature.Contains("human") ? Color.Black : Color.White, ui.DebugSelectedCreature.Contains("human") ? Color.Yellow : new Color(40,40,40));
            WritePill(surf, ref cx, cy, "Goblin", ui.DebugSelectedCreature.Contains("goblin") ? Color.Black : Color.White, ui.DebugSelectedCreature.Contains("goblin") ? Color.Yellow : new Color(40,40,40));
            WritePill(surf, ref cx, cy, "Elf", ui.DebugSelectedCreature.Contains("elf") ? Color.Black : Color.White, ui.DebugSelectedCreature.Contains("elf") ? Color.Yellow : new Color(40,40,40));
            WritePill(surf, ref cx, cy, "Orc", ui.DebugSelectedCreature.Contains("orc") ? Color.Black : Color.White, ui.DebugSelectedCreature.Contains("orc") ? Color.Yellow : new Color(40,40,40));

            surf.Print(x0 + 2, y0 + 9, $"Selected: {GetCreatureName(ui.DebugSelectedCreature)}", Color.Cyan);
            surf.Print(x0 + 2, y0 + 11, "Click map to spawn at mouse position", Color.Green);
        }
        else // Items tab
        {
            surf.Print(x0 + 2, y0 + 2, "Spawn Item:", Color.Yellow);
            // Category pills (mouse only; no hotkeys)
            int catX = x0 + 2;
            foreach (var label in DebugLayoutCalculator.GetCategoryLabels())
            {
                bool active = (label == "Boulders" && ui.DebugItemCat == DebugItemCategory.Boulders)
                           || (label == "Blocks" && ui.DebugItemCat == DebugItemCategory.Blocks)
                           || (label == "Logs" && ui.DebugItemCat == DebugItemCategory.Logs)
                           || (label == "Planks" && ui.DebugItemCat == DebugItemCategory.Planks)
                           || (label == "Tools" && ui.DebugItemCat == DebugItemCategory.Tools)
                           || (label == "Weapons" && ui.DebugItemCat == DebugItemCategory.Weapons)
                           || (label == "Ammo" && ui.DebugItemCat == DebugItemCategory.Ammo)
                           || (label == "Siege" && ui.DebugItemCat == DebugItemCategory.SiegeWeapons);
                WritePill(surf, ref catX, y0 + 3, label, active ? Color.Black : Color.White, active ? Color.Yellow : new Color(40,40,40));
            }

            var itemIds = GetDebugItemsForCategory(world, ui.DebugItemCat).ToList();

            // Show first 10 items (mouse-only; no numeric hotkeys)
            int listY = y0 + 5;
            int shown = 0;
            foreach (var id in itemIds.Take(10))
            {
                bool sel = ui.DebugSelectedItem == id;
                var color = sel ? Color.White : Color.DarkGray;
                surf.Print(x0 + 4, listY, $"{GetItemNameOrId(world, id)}", color);
                listY++;
                shown++;
            }

            surf.Print(x0 + 2, listY + 1, $"Selected: {GetItemNameOrId(world, ui.DebugSelectedItem)}", Color.Cyan);
            surf.Print(x0 + 2, listY + 3, "Click map to spawn at mouse position", Color.Green);
        }
    }

    private static string GetCreatureName(string id)
    {
        return id switch
        {
            "core_race_dwarf" => "Dwarf",
            "core_race_human" => "Human",
            "core_race_goblin" => "Goblin",
            "core_race_elf" => "Elf",
            "core_race_orc" => "Orc",
            _ => "Unknown"
        };
    }

    private static string GetItemName(string id)
    {
        return id switch
        {
            "core_item_boulder_granite" => "Stone",
            "core_item_ingot_iron_wrought" => "Iron Ingot",
            "core_item_log_oak" => "Wood Log",
            "core_tool_mining_pickaxe" => "Pickaxe",
            "core_weapon_sword_short" => "Short Sword",
            _ => "Unknown"
        };
    }

    private static string GetItemNameOrId(HumanFortress.Simulation.World.World? world, string id)
    {
        if (world == null)
        {
            var name = GetItemName(id);
            return name == "Unknown" ? id : name;
        }
        var def = world.Items.GetDefinition(id);
        if (def == null)
        {
            var name = GetItemName(id);
            return name == "Unknown" ? id : name;
        }
        // If name is generic (e.g., "Boulder"/"Block"/"Plank") try append material friendly name
        var baseName = string.IsNullOrWhiteSpace(def.Name) ? "Unknown" : def.Name!;
        if (!string.IsNullOrEmpty(def.FixedMaterial) && IsGenericResourceName(baseName))
        {
            var matNice = MaterialSuffixFriendly(def.FixedMaterial!);
            if (!string.IsNullOrEmpty(matNice))
                return $"{baseName} ({matNice})";
        }
        return baseName;
    }

    private static bool IsGenericResourceName(string name)
    {
        var n = name.ToLowerInvariant();
        return n == "boulder" || n == "block" || n == "plank" || n == "log";
    }

    private static string MaterialSuffixFriendly(string materialId)
    {
        // materialId example: "core_mat_stone_granite" -> "Granite"
        try
        {
            var parts = materialId.Split('_');
            if (parts.Length >= 1)
            {
                var last = parts[^1];
                if (last.Length > 0)
                {
                    return char.ToUpperInvariant(last[0]) + last.Substring(1).Replace('_', ' ');
                }
            }
        }
        catch { }
        return materialId;
    }

    private static System.Collections.Generic.IEnumerable<string> GetDebugItemsForCategory(HumanFortress.Simulation.World.World? world, DebugItemCategory cat)
    {
        if (world == null)
        {
            return cat switch
            {
                DebugItemCategory.Boulders => new[] { "core_item_boulder_granite", "core_item_boulder_marble" },
                DebugItemCategory.Blocks   => new[] { "core_item_block_granite", "core_item_block_marble" },
                DebugItemCategory.Logs     => new[] { "core_item_log_oak", "core_item_log_pine" },
                DebugItemCategory.Planks   => new[] { "core_item_plank_oak", "core_item_plank_pine" },
                DebugItemCategory.Tools    => new[] { "core_tool_mining_pickaxe" },
                DebugItemCategory.Weapons  => new[] { "core_weapon_sword_short" },
                DebugItemCategory.Ammo     => new[] { "core_ammo_arrow" },
                DebugItemCategory.SiegeWeapons => new[] { "core_weapon_cannon_swivel" },
                _ => System.Array.Empty<string>()
            };
        }

        var defs = world.Items.GetAllDefinitions();
        bool Prefix(string s, string p) => s.StartsWith(p);
        return cat switch
        {
            DebugItemCategory.Boulders => defs.Where(d => Prefix(d.Id, "core_item_boulder_")).Select(d => d.Id).OrderBy(s => s),
            DebugItemCategory.Blocks   => defs.Where(d => Prefix(d.Id, "core_item_block_")).Select(d => d.Id).OrderBy(s => s),
            DebugItemCategory.Logs     => defs.Where(d => Prefix(d.Id, "core_item_log_")).Select(d => d.Id).OrderBy(s => s),
            DebugItemCategory.Planks   => defs.Where(d => Prefix(d.Id, "core_item_plank_")).Select(d => d.Id).OrderBy(s => s),
            DebugItemCategory.Tools    => defs.Where(d => Prefix(d.Id, "core_tool_")).Select(d => d.Id).OrderBy(s => s),
            DebugItemCategory.Weapons  => defs.Where(d => d.Kind.Equals("weapon", System.StringComparison.OrdinalIgnoreCase)).Select(d => d.Id).OrderBy(s => s),
            DebugItemCategory.Ammo     => defs.Where(d => d.Kind.Equals("ammo", System.StringComparison.OrdinalIgnoreCase)).Select(d => d.Id).OrderBy(s => s),
            DebugItemCategory.SiegeWeapons => defs.Where(d => d.Kind.Equals("siege_weapon", System.StringComparison.OrdinalIgnoreCase)).Select(d => d.Id).OrderBy(s => s),
            _ => System.Array.Empty<string>()
        };
    }

    // Draw Stockpiles tab content
        // Draw Stockpiles tab content (safe version)
        // Draw Stockpiles tab content (stubbed)
    private static void DrawStockpilesTab(ICellSurface surf, StockpileManager stockpileManager, int startY)
    {
        var zones = stockpileManager.GetAllZones().ToList();
        surf.Print(2, startY, $"Stockpiles: {zones.Count}", Color.Yellow);
        int y = startY + 2;
        foreach (var zone in zones.Take(10))
        {
            surf.Print(2, y++, $"#{zone.ZoneId} {zone.Name} Pri:{zone.Priority}", Color.White);
        }
    }private static void DrawCreaturesTab(ICellSurface surf, HumanFortress.Simulation.World.World world, UiStore ui, int startY, int maxHeight)
    {
        var creatures = world.Creatures.GetAllInstances().ToList();

        surf.Print(2, startY, $"=== All Creatures ({creatures.Count}) ===", Color.Yellow);
        surf.Print(2, startY + 1, "Click creature to view details | Filter: (Coming soon)", Color.Gray);

        if (creatures.Count == 0)
        {
            surf.Print(2, startY + 3, "No creatures spawned yet.", Color.DarkGray);
            surf.Print(2, startY + 4, "Use F12 Debug menu to spawn creatures.", Color.DarkGray);
            return;
        }

        int y = startY + 3;
        int maxY = startY + maxHeight - 2;
        int displayed = 0;

        foreach (var creature in creatures.Take(20)) // Show first 20
        {
            if (y >= maxY) break;

            var def = world.Creatures.GetDefinition(creature.DefinitionId);
            string name = def?.Name ?? "Unknown";
            string status = creature.HP > 0 ? "IDLE" : "DEAD";
            var statusColor = creature.HP > 0 ? Color.Green : Color.Red;
            bool selected = ui.SelectedCreatureGuid == creature.Guid.ToString();
            var bgColor = selected ? new Color(50, 50, 0) : new Color(20, 20, 20);

            // Render line with custom background for selection
            for (int x = 2; x < surf.Width - 2; x++)
                surf.SetGlyph(x, y, ' ', Color.White, bgColor);

            surf.Print(2, y, $"{name,-12} @ ({creature.Position.X,3},{creature.Position.Y,3},{creature.Z,2})", Color.White);
            surf.Print(45, y, $"[{status}]", statusColor);

            y++;
            displayed++;
        }

        if (creatures.Count > displayed)
        {
            surf.Print(2, y, $"... and {creatures.Count - displayed} more (scroll coming soon)", Color.DarkGray);
        }

        // Footer stats
        int alive = creatures.Count(c => c.HP > 0);
        int dead = creatures.Count - alive;
        surf.Print(2, startY + maxHeight - 1, $"Total: {creatures.Count}  Alive: {alive}  Dead: {dead}", Color.Cyan);
    }

    // Draw Animals tab (F1 Tab 1)
    private static void DrawAnimalsTab(ICellSurface surf, int startY)
    {
        surf.Print(2, startY, "=== Animals ===", Color.Yellow);
        surf.Print(2, startY + 2, "Coming soon - Animal tracking will be available here.", Color.Gray);
        surf.Print(2, startY + 3, "Press ESC to return.", Color.DarkGray);
    }

    // Draw Items tab (F2 Tab 0)
        private static void DrawItemsTab(ICellSurface surf, HumanFortress.Simulation.World.World world, UiStore ui, int startY, int maxHeight)
        {
            // Kind filter dropdown
            var availableKinds = new[] { "all", "resource", "weapon", "armor", "tool", "container", "consumable", "placeable", "ammo", "siege_weapon" };
            surf.Print(2, startY, "Filter by kind: [", Color.Gray);
        int filterX = 18;
        foreach (var kind in availableKinds)
        {
            bool active = ui.ItemKindFilter == kind;
            var color = active ? Color.Yellow : Color.DarkGray;
            surf.Print(filterX, startY, kind, color);
            filterX += kind.Length + 1;
            if (kind != availableKinds[^1])
                surf.Print(filterX - 1, startY, "|", Color.Gray);
        }

        // Get items (on ground only)
        var allItems = world.Items.GetAllInstances().Where(i => i.IsOnGround);
        var filteredItems = ui.ItemKindFilter == "all"
            ? allItems.ToList()
            : allItems.Where(item => {
                var def = world.Items.GetDefinition(item.DefinitionId);
                return def?.Kind.ToLower() == ui.ItemKindFilter;
            }).ToList();

        surf.Print(2, startY + 2, $"=== Items on Map ({filteredItems.Count}) ===", Color.Yellow);

        if (filteredItems.Count == 0)
        {
            surf.Print(2, startY + 4, "No items found.", Color.DarkGray);
            surf.Print(2, startY + 5, "Use F12 Debug menu to spawn items.", Color.DarkGray);
            return;
        }

        int y = startY + 4;
        int maxY = startY + maxHeight - 2;
        int displayed = 0;
        int totalUnits = 0;

        foreach (var item in filteredItems.Take(20))
        {
            if (y >= maxY) break;

            var def = world.Items.GetDefinition(item.DefinitionId);
            string name = def?.Name ?? "Unknown";
            // Show material for generic resources (Boulder/Block/Plank/Log)
            if (def != null)
            {
                string? mat = item.MaterialId;
                if (string.IsNullOrEmpty(mat)) mat = def.FixedMaterial; // fallback to definition's fixed material

                // DEBUG: Log material resolution for boulders
                if (def.Id.Contains("boulder"))
                {
                    Logger.Log($"[F2 Items] Boulder debug: id={def.Id} name={name} mat={mat ?? "NULL"} fixedMat={def.FixedMaterial ?? "NULL"} isGeneric={IsGenericResourceName(name)}");
                }

                if (!string.IsNullOrEmpty(mat) && IsGenericResourceName(name))
                {
                    var matNice = MaterialSuffixFriendly(mat!);
                    name = $"{matNice} {name}";
                }
            }
            int qty = item.StackCount;
            totalUnits += qty;
            bool selected = ui.SelectedItemGuid == item.Guid.ToString();
            var bgColor = selected ? new Color(50, 50, 0) : new Color(20, 20, 20);

            // Render line with selection background
            for (int x = 2; x < surf.Width - 2; x++)
                surf.SetGlyph(x, y, ' ', Color.White, bgColor);

            string qtyStr = qty > 1 ? $"x{qty}" : "";
            surf.Print(2, y, $"{name,-15} {qtyStr,-4} @ ({item.Position.X,3},{item.Position.Y,3},{item.Z,2})", Color.White);

            y++;
            displayed++;
        }

        if (filteredItems.Count > displayed)
        {
            surf.Print(2, y, $"... and {filteredItems.Count - displayed} more", Color.DarkGray);
        }

        // Footer stats
        surf.Print(2, startY + maxHeight - 1, $"Total: {filteredItems.Count} items  {totalUnits} units", Color.Cyan);
    }

    // Draw simple debug dwarf markers (overlay) on current z
    public static void DrawDebugUnits(ScreenSurface mapSurface, UiStore ui, int cameraX, int cameraY, int z)
    {
        var surf = mapSurface.Surface;
        foreach (var (pos, dz) in ui.DebugDwarfs)
        {
            if (dz != z) continue;
            int sx = pos.X - cameraX;
            int sy = pos.Y - cameraY;
            if (sx >= 0 && sy >= 0 && sx < surf.Width && sy < surf.Height)
            {
                surf.SetGlyph(sx, sy, 'D', Color.Yellow, new Color(0, 0, 0));
            }
        }
    }

    // Top bar with time/speed hints
    public static void DrawTopBar(ScreenSurface mapSurface, SimulationStatus? simulationStatus = null)
    {
        var surf = mapSurface.Surface;
        int y = 0;
        for (int x = 0; x < surf.Width; x++)
            surf.SetGlyph(x, y, ' ', Color.White, new Color(10, 10, 10));

        // Show current speed/pause status
        string statusText = "";
        Color statusColor = Color.Gray;

        if (simulationStatus.HasValue)
        {
            var status = simulationStatus.Value;
            if (status.IsPaused)
            {
                statusText = "[PAUSED]";
                statusColor = Color.Yellow;
            }
            else
            {
                statusText = $"[{status.SpeedMultiplier:F2}x]";
                statusColor = status.SpeedMultiplier switch
                {
                    < 1.0f => Color.Cyan,      // Slow = cyan
                    1.0f => Color.White,       // Normal = white
                    _ => Color.Orange          // Fast = orange
                };
            }

            surf.Print(1, y, statusText, statusColor);
            surf.Print(1 + statusText.Length + 2, y, "[Space] Pause  [-] Slower  [+] Faster", Color.Gray);
        }
        else
        {
            surf.Print(1, y, "[Space] Pause  [-] Slower  [+] Faster", Color.Gray);
        }

        // if (simulationStatus?.CurrentTick % 50 == 0)
        //     Logger.Log($"[UiRenderer.TopBar] overlay={surf.Width}x{surf.Height} tick={simulationStatus.Value.CurrentTick}");
    }

    private static void DrawButton(ICellSurface surf, ref int x, int y, string label, bool active)
    {
        var fg = active ? Color.Black : Color.White;
        var bg = active ? Color.Yellow : new Color(40, 40, 40);
        for (int i = 0; i < label.Length + 1; i++)
        {
            surf.SetGlyph(x + i, y, ' ', Color.White, bg);
        }
        surf.Print(x + 1, y, label, fg);
        x += label.Length + 2;
    }

        private static void DrawText(ICellSurface surf, int x, int y, string label, bool active)
        {
            var color = active ? Color.Yellow : Color.White;
            surf.Print(x, y, label, color);
        }

        private static void DrawSquareButton(ICellSurface surf, ref int x, int y, string text, bool active, int width)
        {
            var fg = active ? Color.Black : Color.White;
            var bg = active ? Color.Yellow : new Color(40, 40, 40);
            // Draw square block [text] with fixed width
            for (int i = 0; i < width; i++)
            {
                surf.SetGlyph(x + i, y, ' ', Color.White, bg);
            }
            var label = $"{text}";
            int lx = x + Math.Max(0, (width - label.Length) / 2);
            surf.Print(lx, y, label, fg);
            x += width + 1; // 1 space gap
        }

        private static void WritePill(ICellSurface surf, ref int x, int y, string text, Color fg, Color bg)
        {
            surf.SetGlyph(x, y, ' ', Color.White, bg);
            surf.Print(x + 1, y, text, fg);
            x += text.Length + 2;
        surf.SetGlyph(x - 1, y, ' ', Color.White, bg);
    }

    private static void DrawZonesTab(ICellSurface surf, HumanFortress.Simulation.World.World world, int startY, int maxHeight)
    {
        surf.Print(2, startY, "All Zones:", Color.Yellow);

        var zones = world.Zones.Manager.GetAllZones().ToList();
        if (zones.Count == 0)
        {
            surf.Print(4, startY + 2, "No zones created yet", Color.Gray);
            surf.Print(4, startY + 3, "Press X to open zone menu and create zones", Color.DarkGray);
            return;
        }

        int line = startY + 2;
        int maxLines = startY + maxHeight - 2;

        surf.Print(4, line++, $"{"ID",-6} {"Name",-25} {"Type",-20} {"Cells",8}", Color.Gray);

        foreach (var zone in zones.OrderBy(z => z.ZoneId))
        {
            if (line >= maxLines) break;

            var def = world.Zones.Manager.GetDefinition(zone.DefId);
            string typeName = def?.DisplayName ?? zone.DefId;
            string name = zone.Name.Length > 24 ? zone.Name.Substring(0, 21) + "..." : zone.Name;
            string type = typeName.Length > 19 ? typeName.Substring(0, 16) + "..." : typeName;

            surf.Print(4, line++, $"{zone.ZoneId,-6} {name,-25} {type,-20} {zone.TotalCells,8}", Color.White);
        }

        if (zones.Count > (maxLines - startY - 2))
        {
            surf.Print(4, maxLines, $"... and {zones.Count - (maxLines - startY - 2)} more", Color.DarkGray);
        }
    }
}
