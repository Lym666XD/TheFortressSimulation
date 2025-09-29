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
            if (tick % 50 == 0)
                Logger.Log($"[UiRenderer.DockScreen] overlay={surf.Width}x{surf.Height} row={y}");

            DrawSquareButton(surf, ref x, y, "F1", ui.OpenDrawer == DrawerId.Creature, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F2", ui.OpenDrawer == DrawerId.Stock, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F3", ui.OpenDrawer == DrawerId.Work, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F4", ui.OpenDrawer == DrawerId.Military, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F5", ui.OpenDrawer == DrawerId.Country, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F6", ui.OpenDrawer == DrawerId.World, buttonWidth);
            DrawSquareButton(surf, ref x, y, "F7", ui.OpenDrawer == DrawerId.Log, buttonWidth);
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
        DrawButton(surf, ref x, y, "[F4]", ui.OpenDrawer == DrawerId.Military);
        DrawButton(surf, ref x, y, "[F5]", ui.OpenDrawer == DrawerId.Country);
        DrawButton(surf, ref x, y, "[F6]", ui.OpenDrawer == DrawerId.World);
        DrawButton(surf, ref x, y, "[F7]", ui.OpenDrawer == DrawerId.Log);
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

        // Draw quick icons centered one row above the bottom (overlay)
        public static void DrawQuickIconsScreen(ScreenSurface overlay, UiStore ui, ulong tick)
        {
            var surf = overlay.Surface;
            int y = surf.Height - 2; // one row above bottom so drawer may cover it
            int center = surf.Width / 2;
            int buttonWidth = 5;
            int gap = 2;
            int xZ = center - (buttonWidth + gap) - buttonWidth / 2;
            int xX = center - buttonWidth / 2;
            int xC = center + (buttonWidth + gap) - buttonWidth / 2;

            DrawSquareButton(surf, ref xZ, y, "Z", ui.QuickMenu == QuickMenuKind.Orders, buttonWidth);
            xX = center - buttonWidth / 2; // reset exact center after ref move
            DrawSquareButton(surf, ref xX, y, "X", ui.QuickMenu == QuickMenuKind.Zones, buttonWidth);
            xC = center + (buttonWidth + gap) - buttonWidth / 2;
            DrawSquareButton(surf, ref xC, y, "C", ui.QuickMenu == QuickMenuKind.Build, buttonWidth);

            if (tick % 50 == 0)
                Logger.Log($"[UiRenderer.QuickIconsScreen] overlay={surf.Width}x{surf.Height} row={y} center={center} width={buttonWidth}");
        }

    // Draw bottom drawer placeholder
    public static void DrawDrawer(ScreenSurface mapSurface, UiStore ui, ulong tick, StockpileManager? stockpileManager = null)
    {
        if (ui.OpenDrawer == DrawerId.None) return;
        var surf = mapSurface.Surface;
        int height = Math.Max(8, (int)(surf.Height * 0.7));
        int y0 = surf.Height - 1 - height; // top of drawer area
        if (tick % 50 == 0)
            Logger.Log($"[UiRenderer.Drawer] size={surf.Width}x{surf.Height} height={height} y0={y0} panel={ui.OpenDrawer} tab={ui.DrawerTab}");
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
            DrawerId.Stock => "Stock Management",
            DrawerId.Work => "Work Management",
            DrawerId.Military => "Military Management",
            DrawerId.Country => "Country Management",
            DrawerId.World => "World Map / Diplomacy",
            DrawerId.Log => "Log / Messages / History",
            _ => "Panel"
        };
        surf.Print(1, y0, $"== {title} ==", Color.Yellow);

        // 为Stock面板自定义tabs
        string[] tabs;
        if (ui.OpenDrawer == DrawerId.Stock)
        {
            tabs = new[] { "Items", "Stockpiles", "Trade" };
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

        // Stock面板的Stockpiles tab内容
        if (ui.OpenDrawer == DrawerId.Stock && ui.DrawerTab == 1 && stockpileManager != null)
        {
            DrawStockpilesTab(surf, stockpileManager, y0 + 2);
        }
        else
        {
            // content placeholder
            surf.Print(2, y0 + 2, "(Content coming soon)", Color.Gray);
        }
    }

    // Draw quick menu (root only, minimal)
    public static void DrawQuickMenu(ScreenSurface mapSurface, UiStore ui, ulong tick)
    {
        if (ui.QuickMenu == QuickMenuKind.None) return;
        var surf = mapSurface.Surface;
        int width = surf.Width - 4;
        int height = Math.Max(8, (int)(surf.Height * 0.7));
        int x0 = 2;
        int y0 = surf.Height - 2 - height; // above dock row
        if (tick % 50 == 0)
            Logger.Log($"[UiRenderer.QuickMenu] size={surf.Width}x{surf.Height} x0={x0} y0={y0} height={height} kind={ui.QuickMenu}");
        for (int y = y0; y < y0 + height; y++)
        {
            for (int x = x0; x < x0 + width; x++)
            {
                surf.SetGlyph(x, y, ' ', Color.White, new Color(25, 25, 25));
            }
        }
        string title = ui.QuickMenu switch
        {
            QuickMenuKind.Orders => "Orders",
            QuickMenuKind.Zones => "Zones",
            QuickMenuKind.Build => "Build",
            _ => "Menu"
        };
        surf.Print(x0 + 2, y0, $"-- {title} --", Color.Cyan);
        // Minimal stub buttons
        surf.Print(x0 + 2, y0 + 2, "[WIP] Buttons here...", Color.Gray);
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

    public static void DrawDebug(ScreenSurface mapSurface, UiStore ui, SadRogue.Primitives.Point cursor, int currentZ, int zoomLevel, SadRogue.Primitives.Point camera, int fortressSize)
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

        // Title
        surf.Print(x0 + 2, y0, "DEBUG", Color.Cyan);

        // Tab buttons
        surf.Print(x0 + 20, y0, "[TAB] Switch", Color.Gray);
        var tabColor0 = ui.DebugMenuTab == 0 ? Color.Yellow : Color.DarkGray;
        var tabColor1 = ui.DebugMenuTab == 1 ? Color.Yellow : Color.DarkGray;
        surf.Print(x0 + 35, y0, "[1] Creatures", tabColor0);
        surf.Print(x0 + 50, y0, "[2] Items", tabColor1);

        // Left column - spawn options based on tab
        if (ui.DebugMenuTab == 0) // Creatures tab
        {
            surf.Print(x0 + 2, y0 + 2, "Spawn Creature (click):", Color.Yellow);
            surf.Print(x0 + 2, y0 + 3, "[D] Dwarf", ui.DebugSelectedCreature.Contains("dwarf") ? Color.White : Color.DarkGray);
            surf.Print(x0 + 2, y0 + 4, "[H] Human", ui.DebugSelectedCreature.Contains("human") ? Color.White : Color.DarkGray);
            surf.Print(x0 + 2, y0 + 5, "[G] Goblin", ui.DebugSelectedCreature.Contains("goblin") ? Color.White : Color.DarkGray);
            surf.Print(x0 + 2, y0 + 6, "[E] Elf", ui.DebugSelectedCreature.Contains("elf") ? Color.White : Color.DarkGray);
            surf.Print(x0 + 2, y0 + 7, "[O] Orc", ui.DebugSelectedCreature.Contains("orc") ? Color.White : Color.DarkGray);
            surf.Print(x0 + 2, y0 + 9, $"Selected: {GetCreatureName(ui.DebugSelectedCreature)}", Color.Cyan);
        }
        else // Items tab
        {
            surf.Print(x0 + 2, y0 + 2, "Spawn Item (click):", Color.Yellow);
            surf.Print(x0 + 2, y0 + 3, "[1] Stone", ui.DebugSelectedItem.Contains("stone") ? Color.White : Color.DarkGray);
            surf.Print(x0 + 2, y0 + 4, "[2] Iron Ingot", ui.DebugSelectedItem.Contains("iron") ? Color.White : Color.DarkGray);
            surf.Print(x0 + 2, y0 + 5, "[3] Wood Log", ui.DebugSelectedItem.Contains("wood") ? Color.White : Color.DarkGray);
            surf.Print(x0 + 2, y0 + 6, "[4] Pickaxe", ui.DebugSelectedItem.Contains("pickaxe") ? Color.White : Color.DarkGray);
            surf.Print(x0 + 2, y0 + 7, "[5] Sword", ui.DebugSelectedItem.Contains("sword") ? Color.White : Color.DarkGray);
            surf.Print(x0 + 2, y0 + 9, $"Selected: {GetItemName(ui.DebugSelectedItem)}", Color.Cyan);
        }

        // Click to spawn button
        surf.Print(x0 + 2, y0 + 11, "[CLICK] Spawn at cursor", Color.Green);

        // Status and map info (right column)
        int col2 = x0 + Math.Max(35, width / 2);
        surf.Print(col2, y0 + 2, "Status:", Color.Yellow);
        surf.Print(col2, y0 + 3, "Simulation: Running", Color.Green);
        surf.Print(col2, y0 + 4, "TPS: 50", Color.Green);

        surf.Print(col2, y0 + 6, $"Cursor: {cursor.X},{cursor.Y}", Color.White);
        surf.Print(col2, y0 + 7, $"Z-Level: {currentZ}/49", Color.Cyan);
        surf.Print(col2, y0 + 8, $"Zoom: {zoomLevel}x", Color.White);
        surf.Print(col2, y0 + 9, $"Camera: {camera.X},{camera.Y}", Color.Gray);
        surf.Print(col2, y0 + 10, $"Map: {fortressSize}x{fortressSize} chunks", Color.Gray);
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
            "core_item_stone_generic" => "Stone",
            "core_item_ingot_iron" => "Iron Ingot",
            "core_item_wood_log" => "Wood Log",
            "core_tool_mining_pickaxe" => "Pickaxe",
            "core_weapon_sword_short" => "Short Sword",
            _ => "Unknown"
        };
    }

    // Draw Stockpiles tab content
    private static void DrawStockpilesTab(ICellSurface surf, StockpileManager stockpileManager, int startY)
    {
        var zones = stockpileManager.GetAllZones().ToList();

        if (zones.Count == 0)
        {
            surf.Print(2, startY, "No stockpiles created yet.", Color.Gray);
            surf.Print(2, startY + 2, "Press [X] -> [Z] -> [Z] to create a stockpile.", Color.DarkGray);
            return;
        }

        surf.Print(2, startY, "Stockpile Zones:", Color.Yellow);

        int y = startY + 2;
        foreach (var zone in zones.Take(15)) // 最多显示15个
        {
            var priorityColor = zone.Priority switch
            {
                3 => Color.Red,
                2 => Color.Orange,
                1 => Color.Yellow,
                _ => Color.Gray
            };

            surf.Print(2, y, $"#{zone.ZoneId} {zone.Name}", Color.White);
            surf.Print(30, y, $"Pri:{zone.Priority}", priorityColor);
            surf.Print(40, y, $"Chunks:{zone.MemberChunks.Count}", Color.Gray);

            // 显示过滤器摘要
            if (zone.Filter.Tags.Count > 0)
            {
                var tags = string.Join(", ", zone.Filter.Tags.Take(3));
                surf.Print(50, y, tags, Color.DarkGray);
            }
            else
            {
                surf.Print(50, y, "All items", Color.DarkGray);
            }

            y++;
        }

        if (zones.Count > 15)
        {
            surf.Print(2, y, $"... and {zones.Count - 15} more", Color.DarkGray);
        }
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
    public static void DrawTopBar(ScreenSurface mapSurface, ulong tick)
    {
        var surf = mapSurface.Surface;
        int y = 0;
        for (int x = 0; x < surf.Width; x++)
            surf.SetGlyph(x, y, ' ', Color.White, new Color(10, 10, 10));
        surf.Print(1, y, "[Space] Pause  [-] Slower  [+] Faster", Color.Gray);
        if (tick % 50 == 0)
            Logger.Log($"[UiRenderer.TopBar] overlay={surf.Width}x{surf.Height} tick={tick}");
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
}


