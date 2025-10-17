using SadConsole;
using SadRogue.Primitives;
using HumanFortress.App;
using HumanFortress.Simulation.Stockpile;
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
    public static void DrawDrawer(ScreenSurface mapSurface, UiStore ui, ulong tick, StockpileManager? stockpileManager = null, HumanFortress.Simulation.World.World? world = null)
    {
        if (ui.OpenDrawer == DrawerId.None) return;
        var surf = mapSurface.Surface;
        int height = Math.Max(8, (int)(surf.Height * 0.7));
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
            tabs = new[] { "Labor", "All Orders", "Settings" };
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
            if (ui.DrawerTab == 0 && world != null)
            {
                DrawWorkOverviewTab(surf, y0 + 2);
            }
            else if (ui.DrawerTab == 1 && world != null)
            {
                DrawWorkOrdersTab(surf, world, y0 + 2);
            }
            else
            {
                surf.Print(2, y0 + 2, "(Configure coming soon)", Color.Gray);
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

    private static void DrawWorkOrdersTab(ICellSurface surf, HumanFortress.Simulation.World.World world, int startY)
    {
        var gsm = HumanFortress.App.GameStates.GameStateManager.Instance;
        surf.Print(2, startY, "== Work: All Orders (Detailed) ==", Color.Yellow);
        int y = startY + 2;

        // === HAUL ORDERS ===
        var haulOrders = world.Orders.GetRecentHauls();
        var haulActive = world.Orders.GetActiveHaulsSnapshot();
        var haulJobs = gsm.TransportJobs?.GetActiveJobsSnapshot() ?? new System.Collections.Generic.List<HumanFortress.App.Jobs.TransportJobSystem.ActiveJobView>();

        surf.Print(2, y++, $"[HAUL] Recent Orders: {haulOrders.Count}  Active Designations: {haulActive.Count}  Active Jobs: {haulJobs.Count}", Color.Cyan);

        // Show haul jobs with details
        if (haulJobs.Count > 0)
        {
            surf.Print(4, y++, "Active Haul Jobs:", Color.Green);
            foreach (var job in haulJobs.Take(5))
            {
                var workerShort = job.CreatureId.ToString().Substring(0, 8);
                var itemShort = job.ItemId.ToString().Substring(0, 8);
                surf.Print(6, y++, $"W:{workerShort} I:{itemShort} Stage:{job.Stage} -> ({job.Dest.X},{job.Dest.Y},{job.Dest.Z})", Color.White);
            }
            if (haulJobs.Count > 5)
                surf.Print(6, y++, $"... and {haulJobs.Count - 5} more haul jobs", Color.DarkGray);
        }

        // Show recent haul orders
        if (haulOrders.Count > 0)
        {
            surf.Print(4, y++, "Recent Haul Designations:", Color.Green);
            foreach (var d in haulOrders.Take(3))
            {
                surf.Print(6, y++, $"Rect ({d.WorldRect.X},{d.WorldRect.Y}) {d.WorldRect.Width}x{d.WorldRect.Height} z={d.Z} pri:{d.Priority}", Color.White);
            }
            if (haulOrders.Count > 3)
                surf.Print(6, y++, $"... and {haulOrders.Count - 3} more designations", Color.DarkGray);
        }
        y++;

        // === MINING ORDERS ===
        var miningOrders = world.Orders.GetRecentMining();
        var miningActive = world.Orders.GetActiveMiningSnapshot();
        var miningJobs = gsm.MiningJobs?.GetActiveJobsSnapshot() ?? new System.Collections.Generic.List<HumanFortress.App.Jobs.MiningJobSystem.ActiveMiningJobView>();

        surf.Print(2, y++, $"[MINING] Recent Orders: {miningOrders.Count}  Active Designations: {miningActive.Count}  Active Jobs: {miningJobs.Count}", Color.Cyan);

        // Show mining jobs with details
        if (miningJobs.Count > 0)
        {
            surf.Print(4, y++, "Active Mining Jobs:", Color.Green);
            foreach (var job in miningJobs.Take(5))
            {
                var workerShort = job.WorkerId.ToString().Substring(0, 8);
                var progress = job.RequiredTicks > 0 ? (job.ProgressTicks * 100 / job.RequiredTicks) : 0;
                surf.Print(6, y++, $"W:{workerShort} Target:({job.Target.X},{job.Target.Y},{job.Z}) Stage:{job.Stage} Progress:{progress}%", Color.White);
            }
            if (miningJobs.Count > 5)
                surf.Print(6, y++, $"... and {miningJobs.Count - 5} more mining jobs", Color.DarkGray);
        }

        // Show recent mining orders (V2)
        if (miningOrders.Count > 0)
        {
            surf.Print(4, y++, "Recent Mining Designations:", Color.Green);
            foreach (var d in miningOrders.Take(3))
            {
                surf.Print(6, y++, $"Rect ({d.Rect.X},{d.Rect.Y}) {d.Rect.Width}x{d.Rect.Height} z={d.ZMin}..{d.ZMax} action={d.Action} pri:{d.Priority}", Color.White);
            }
            if (miningOrders.Count > 3)
                surf.Print(6, y++, $"... and {miningOrders.Count - 3} more designations", Color.DarkGray);
        }
        y++;

        // === JOB STATS ===
        try
        {
            surf.Print(2, y++, "[STATS] Haul: Assigned:{0} Completed:{1} NoPath:{2} Requeued:{3}", Color.Yellow);
            surf.Print(2, y - 1, $"[STATS] Haul: Assigned:{HumanFortress.App.Jobs.JobStats.Assigned} Completed:{HumanFortress.App.Jobs.JobStats.Completed} NoPath:{HumanFortress.App.Jobs.JobStats.NoPath} Requeued:{HumanFortress.App.Jobs.JobStats.Requeued}", Color.Yellow);
        }
        catch { /* if not available, skip */ }

        if (haulOrders.Count == 0 && miningOrders.Count == 0 && haulJobs.Count == 0 && miningJobs.Count == 0)
        {
            surf.Print(2, startY + 4, "No work orders yet. Use Z menu to create orders.", Color.Gray);
        }
    }

    private static void DrawWorkOverviewTab(ICellSurface surf, int startY)
    {
        surf.Print(2, startY, "== Work: Creature Job Assignment ==", Color.Yellow);
        surf.Print(2, startY + 2, "Reserved for future creature work assignment system.", Color.Gray);
        surf.Print(2, startY + 4, "This tab will allow you to:", Color.DarkGray);
        surf.Print(4, startY + 5, "- Assign creatures to specific job types", Color.DarkGray);
        surf.Print(4, startY + 6, "- Set work priorities per creature", Color.DarkGray);
        surf.Print(4, startY + 7, "- Manage labor allocation", Color.DarkGray);
        surf.Print(2, startY + 9, "Use Tab 1 to view all active orders.", Color.Cyan);
    }

    // Draw quick menu (new compact popup-based UI)
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
    public static void DrawTopBar(ScreenSurface mapSurface, ulong tick, HumanFortress.Core.Time.TickScheduler? scheduler = null)
    {
        var surf = mapSurface.Surface;
        int y = 0;
        for (int x = 0; x < surf.Width; x++)
            surf.SetGlyph(x, y, ' ', Color.White, new Color(10, 10, 10));

        // Show current speed/pause status
        string statusText = "";
        Color statusColor = Color.Gray;

        if (scheduler != null)
        {
            if (scheduler.IsPaused)
            {
                statusText = "[PAUSED]";
                statusColor = Color.Yellow;
            }
            else
            {
                statusText = $"[{scheduler.SpeedMultiplier:F2}x]";
                statusColor = scheduler.SpeedMultiplier switch
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

        // if (tick % 50 == 0)
        //     Logger.Log($"[UiRenderer.TopBar] overlay={surf.Width}x{surf.Height} tick={tick}");
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

