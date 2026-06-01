using SadConsole;
using SadConsole.Components;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.App.Jobs;
using HumanFortress.App.UI.Commands;
using HumanFortress.App.UI;
using HumanFortress.Simulation.World;
using System.Linq;

namespace HumanFortress.App.UI.Components
{
    /// <summary>
    /// SadConsole component that handles all UI input (mouse + keyboard).
    /// Converts input events into UI commands and executes them through UIStateManager.
    /// Replaces the scattered click handlers in FortressState.
    /// </summary>
    public sealed class InputHandlerComponent : IComponent
    {
        private readonly UIStateManager _uiStateManager;
        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private readonly Func<ulong> _uiTickProvider;
        private readonly Func<World?> _worldProvider;
        private readonly Func<ProfessionAssignments?> _professionAssignmentsProvider;
        private readonly Func<IReadOnlyList<ProfessionAssignments.ProfessionRosterEntry>> _professionRosterProvider;
        private readonly Action<Guid, string, int> _setProfessionWeight;

        // Drawer ID mapping for F1-F8 buttons
        private static readonly DrawerId[] DockButtonDrawers = new[]
        {
            DrawerId.Creature,              // F1 (slot 0)
            DrawerId.Stock,                 // F2 (slot 1)
            DrawerId.Work,                  // F3 (slot 2)
            DrawerId.PlacementManagement,   // F4 (slot 3)
            DrawerId.Military,              // F5 (slot 4)
            DrawerId.Country,               // F6 (slot 5)
            DrawerId.World,                 // F7 (slot 6)
            DrawerId.Log                    // F8 (slot 7)
        };

        // Quick menu mapping for Z/X/C/V buttons
        private static readonly QuickMenuKind[] QuickButtonMenus = new[]
        {
            QuickMenuKind.Orders,      // Z (index 0)
            QuickMenuKind.Zones,       // X (index 1)
            QuickMenuKind.Build,       // C (index 2)
            QuickMenuKind.Stockpile    // V (index 3)
        };

        public InputHandlerComponent(
            UIStateManager uiStateManager,
            int screenWidth,
            int screenHeight,
            Func<ulong>? uiTickProvider = null,
            Func<World?>? worldProvider = null,
            Func<ProfessionAssignments?>? professionAssignmentsProvider = null,
            Func<IReadOnlyList<ProfessionAssignments.ProfessionRosterEntry>>? professionRosterProvider = null,
            Action<Guid, string, int>? setProfessionWeight = null)
        {
            _uiStateManager = uiStateManager;
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _uiTickProvider = uiTickProvider ?? (() => 0UL);
            _worldProvider = worldProvider ?? (() => null);
            _professionAssignmentsProvider = professionAssignmentsProvider ?? (() => null);
            _professionRosterProvider = professionRosterProvider ?? (() => Array.Empty<ProfessionAssignments.ProfessionRosterEntry>());
            _setProfessionWeight = setProfessionWeight ?? ((_, _, _) => { });
        }

        private void AddToast(string text, ulong durationTicks)
        {
            _uiStateManager.AddToast(text, _uiTickProvider() + durationTicks);
        }

        public void OnAdded(IScreenObject host)
        {
            // Component added to host
        }

        public void OnRemoved(IScreenObject host)
        {
            // Component removed from host
        }

        public void ProcessKeyboard(IScreenObject host, Keyboard keyboard, out bool handled)
        {
            handled = false;

            // F1-F8: Toggle drawers
            if (keyboard.IsKeyPressed(Keys.F1))
            {
                new ToggleDrawerCommand(DrawerId.Creature).Execute(_uiStateManager);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.F2))
            {
                new ToggleDrawerCommand(DrawerId.Stock).Execute(_uiStateManager);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.F3))
            {
                new ToggleDrawerCommand(DrawerId.Work).Execute(_uiStateManager);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.F4))
            {
                new ToggleDrawerCommand(DrawerId.PlacementManagement).Execute(_uiStateManager);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.F5))
            {
                new ToggleDrawerCommand(DrawerId.Military).Execute(_uiStateManager);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.F6))
            {
                new ToggleDrawerCommand(DrawerId.Country).Execute(_uiStateManager);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.F7))
            {
                new ToggleDrawerCommand(DrawerId.World).Execute(_uiStateManager);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.F8))
            {
                new ToggleDrawerCommand(DrawerId.Log).Execute(_uiStateManager);
                handled = true;
            }
            // Z/X/C/V: Toggle quick menus
            else if (keyboard.IsKeyPressed(Keys.Z))
            {
                new ToggleQuickMenuCommand(QuickMenuKind.Orders).Execute(_uiStateManager);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.X))
            {
                new ToggleQuickMenuCommand(QuickMenuKind.Zones).Execute(_uiStateManager);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.C))
            {
                new ToggleQuickMenuCommand(QuickMenuKind.Build).Execute(_uiStateManager);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.V))
            {
                new ToggleQuickMenuCommand(QuickMenuKind.Stockpile).Execute(_uiStateManager);
                handled = true;
            }
            else if (HandleWorkPanelKeys(keyboard))
            {
                handled = true;
            }
            // Escape: Cancel
            else if (keyboard.IsKeyPressed(Keys.Escape))
            {
                new CancelCommand().Execute(_uiStateManager);
                handled = true;
            }
            // Backspace: Navigate back (hierarchical)
            else if (keyboard.IsKeyPressed(Keys.Back))
            {
                new NavigateBackCommand().Execute(_uiStateManager);
                handled = true;
            }
            // F9: Toggle help
            else if (keyboard.IsKeyPressed(Keys.F9))
            {
                new ToggleHelpCommand().Execute(_uiStateManager);
                handled = true;
            }
            // F10: Toggle debug
            else if (keyboard.IsKeyPressed(Keys.F10))
            {
                new ToggleDebugCommand().Execute(_uiStateManager);
                handled = true;
            }
            // F11: Toggle pause (if applicable)
            else if (keyboard.IsKeyPressed(Keys.F11))
            {
                new TogglePauseCommand().Execute(_uiStateManager);
                handled = true;
            }
        }

        public void ProcessMouse(IScreenObject host, MouseScreenObjectState state, out bool handled)
        {
            handled = false;

            var localPos = state.SurfaceCellPosition;

            // Left click
            if (state.Mouse.LeftClicked)
            {
                // Highest priority: Debug overlay interactions when open
                if (TryHandleDebugClick(localPos)) { handled = true; return; }
                handled = HandleLeftClick(localPos);
            }
            // Right click
            else if (state.Mouse.RightClicked)
            {
                handled = HandleRightClick(localPos);
            }
        }

        private bool HandleLeftClick(Point localPos)
        {
            var store = _uiStateManager.Store;

            // Hit-test dock buttons (F1-F8)
            int? dockSlot = ButtonLayoutCalculator.HitTestDockButtons(localPos, _screenWidth, _screenHeight);
            if (dockSlot.HasValue && dockSlot.Value < DockButtonDrawers.Length)
            {
                var drawerId = DockButtonDrawers[dockSlot.Value];
                new ToggleDrawerCommand(drawerId).Execute(_uiStateManager);
                Logger.Log($"[InputHandler] Dock button {dockSlot.Value} -> {drawerId}");
                store.SuppressNextTileClick = true;
                return true;
            }

            // Hit-test quick buttons (Z/X/C/V)
            int? quickSlot = ButtonLayoutCalculator.HitTestQuickButtons(localPos, _screenWidth, _screenHeight);
            if (quickSlot.HasValue && quickSlot.Value < QuickButtonMenus.Length)
            {
                var menuKind = QuickButtonMenus[quickSlot.Value];
                new ToggleQuickMenuCommand(menuKind).Execute(_uiStateManager);
                Logger.Log($"[InputHandler] Quick button {quickSlot.Value} -> {menuKind}");
                store.SuppressNextTileClick = true;
                return true;
            }

            // Hit-test drawer tabs (when any drawer is open)
            if (_uiStateManager.OpenDrawer != DrawerId.None)
            {
                // Calculate drawer geometry (matches UiRenderer.DrawDrawer)
                int drawerHeight = Math.Max(10, _screenHeight - 7);
                int drawerTopY = _screenHeight - 1 - drawerHeight;

                // Get tab labels for current drawer
                string[] tabs = _uiStateManager.OpenDrawer switch
                {
                    DrawerId.Creature => new[] { "All Creatures", "Animals", "Settings" },
                    DrawerId.Stock => new[] { "Items", "Stockpiles", "Trade" },
                    DrawerId.Work => new[] { "Labor", "All Orders", "Job Allocation", "Workshop Orders", "Workshops" },
                    DrawerId.PlacementManagement => new[] { "Zones", "Stockpiles", "Settings" },
                    _ => new[] { "Tab 1", "Tab 2", "Tab 3" }
                };

                int? tabIndex = ButtonLayoutCalculator.HitTestDrawerTabs(localPos, _screenWidth, _screenHeight, tabs, drawerTopY);
                if (tabIndex.HasValue && tabIndex.Value < tabs.Length)
                {
                    _uiStateManager.Store.SetDrawerTab(tabIndex.Value);
                    Logger.Log($"[InputHandler] Drawer tab clicked: {tabs[tabIndex.Value]} (index {tabIndex.Value})");
                    store.SuppressNextTileClick = true;
                    return true;
                }
            }

            // Hit-test F2 Items tab filter pills (when F2 Stock drawer is open on Items tab)
            if (_uiStateManager.OpenDrawer == DrawerId.Stock && _uiStateManager.DrawerTab == 0)
            {
                // Calculate drawer geometry (matches UiRenderer.DrawDrawer)
                int drawerHeight = Math.Max(10, _screenHeight - 7);
                int drawerTopY = _screenHeight - 1 - drawerHeight;
                int filterRowY = drawerTopY + 2; // Items tab content starts at y0+2 (line 167 in UiRenderer)

                // Available kind filters (must match UiRenderer.DrawItemsTab line 894)
                string[] availableKinds = new[] { "all", "resource", "weapon", "armor", "tool", "container", "consumable", "placeable", "ammo", "siege_weapon" };

                int? filterIndex = ButtonLayoutCalculator.HitTestItemKindFilterPills(localPos, _screenWidth, _screenHeight, availableKinds, filterRowY);
                if (filterIndex.HasValue && filterIndex.Value < availableKinds.Length)
                {
                    string newFilter = availableKinds[filterIndex.Value];
                    _uiStateManager.Store.ItemKindFilter = newFilter;
                    Logger.Log($"[InputHandler] F2 filter changed to: {newFilter}");

                    // Optional: Add toast feedback
                    AddToast($"Filter: {newFilter}", 50);
                    store.SuppressNextTileClick = true;
                    return true;
                }
            }

            if (HandleJobAllocationClick(localPos))
            {
                store.SuppressNextTileClick = true;
                return true;
            }

            // TODO: Hit-test drawer tabs, quick menu items, etc.
            // For now, return false to allow other handlers to process

            return false;
        }

        private bool HandleRightClick(Point localPos)
        {
            // Right-click only handles UI navigation when menus are open
            // Otherwise, let FortressState handle it (for tile panel, placement, etc.)

            // Check if any UI menu is open
            new NavigateBackCommand().Execute(_uiStateManager);
            Logger.Log("[InputHandler] Right-click -> NavigateBack (force cancel)");
            _uiStateManager.Store.SuppressNextTileClick = true;
            return true;
        }

        /// <summary>
        /// Handle Debug overlay mouse clicks (category pills &amp; item rows) when Debug panel is open.
        /// Returns true if the click is consumed by the UI.
        /// </summary>
        private bool TryHandleDebugClick(Point localPos)
        {
            var ui = _uiStateManager.Store;
            var worldRef = _worldProvider();
            if (!ui.DebugOpen) return false;

            // Compute window rect to hit-test
            var win = DebugLayoutCalculator.CalculateWindow(_screenWidth, _screenHeight);
            if (!win.Contains(localPos)) return false; // click outside debug panel -> not handled here

            // Hit-test top tabs
            var tabHits = DebugLayoutCalculator.CalculateTabs(win);
            for (int i = 0; i < tabHits.Length; i++)
            {
                if (tabHits[i].Contains(localPos))
                {
                    _uiStateManager.Store.DebugMenuTab = i; // 0=Status,1=Creatures,2=Items
                    AddToast($"Tab: {(i==0?"Status": i==1?"Creatures":"Items")}", 50);
                    return true;
                }
            }

            // Hit-test creature selection (Creatures tab)
            if (ui.DebugMenuTab == 1)
            {
                string[] cLabels = { "Dwarf", "Human", "Goblin", "Elf", "Orc" };
                var cHits = DebugLayoutCalculator.CalculateCategoryPills(win, cLabels);
                for (int i = 0; i < cHits.Length; i++)
                {
                    if (cHits[i].Contains(localPos))
                    {
                        string id = i switch { 0 => "core_race_dwarf", 1 => "core_race_human", 2 => "core_race_goblin", 3 => "core_race_elf", _ => "core_race_orc" };
                        ui.DebugSelectedCreature = id;
                        AddToast($"Creature: {cLabels[i]}", 50);
                        return true;
                    }
                }
            }

            // Hit-test category pills (Items tab)
            string[] labels = { "Boulders", "Blocks", "Logs", "Planks", "Tools", "Weapons", "Ammo", "Siege" };
            var pillHits = DebugLayoutCalculator.CalculateCategoryPills(win, labels);
            for (int i = 0; i < pillHits.Length; i++)
            {
                if (pillHits[i].Contains(localPos))
                {
                    var newCat = i switch
                    {
                        0 => DebugItemCategory.Boulders,
                        1 => DebugItemCategory.Blocks,
                        2 => DebugItemCategory.Logs,
                        3 => DebugItemCategory.Planks,
                        4 => DebugItemCategory.Tools,
                        5 => DebugItemCategory.Weapons,
                        6 => DebugItemCategory.Ammo,
                        _ => DebugItemCategory.SiegeWeapons
                    };
                    ui.DebugItemCat = newCat;

                    // Select first item in this category if available
                    var ids = GetCategoryItemIds(worldRef, newCat).ToList();
                    if (ids.Count > 0) ui.DebugSelectedItem = ids[0];

                    // Visual feedback via toast (flash-like hint)
                    AddToast($"Category: {labels[i]}", 50);
                    return true;
                }
            }

            // Hit-test page buttons
            var pageButtons = DebugLayoutCalculator.CalculatePageButtons(win);
            if (pageButtons[0].Contains(localPos) || pageButtons[1].Contains(localPos))
            {
                var idsAll = GetCategoryItemIds(worldRef, ui.DebugItemCat).ToList();
                int pageSize = 10; int maxPage = idsAll.Count > 0 ? (idsAll.Count - 1) / pageSize : 0;
                if (pageButtons[0].Contains(localPos)) ui.DebugItemPage = System.Math.Max(0, ui.DebugItemPage - 1);
                else ui.DebugItemPage = System.Math.Min(maxPage, ui.DebugItemPage + 1);
                int offset = ui.DebugItemPage * pageSize; if (idsAll.Count > offset) ui.DebugSelectedItem = idsAll[offset];
                AddToast($"Page {ui.DebugItemPage + 1}/{maxPage + 1}", 50);
                return true;
            }

            // Hit-test list rows (up to 10 visible) 鈥?page-aware
            var rowHits = DebugLayoutCalculator.CalculateItemRows(win, 10);
            var all = GetCategoryItemIds(worldRef, ui.DebugItemCat).ToList();
            int pageSize2 = 10; int offset2 = ui.DebugItemPage * pageSize2;
            var list = all.Skip(offset2).Take(10).ToList();
            for (int i = 0; i < rowHits.Length && i < list.Count; i++)
            {
                if (rowHits[i].Contains(localPos))
                {
                    ui.DebugSelectedItem = list[i];
                    // Subtle toast
                    AddToast($"Selected: {list[i]}", 40);
                    return true;
                }
            }

            // Click inside debug window but not on interactive elements -> consume (avoid spawning on UI)
            return true;
        }

        private static System.Collections.Generic.IEnumerable<string> GetCategoryItemIds(HumanFortress.Simulation.World.World? world, DebugItemCategory cat)
        {
            if (world == null) return System.Array.Empty<string>();
            var defs = world.Items.GetAllDefinitions();
            static bool Prefix(string s, string p) => s.StartsWith(p);
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

        public void Render(IScreenObject host, TimeSpan delta)
        {
            // No rendering needed
        }

        public void Update(IScreenObject host, TimeSpan delta)
        {
            // No update logic needed
        }

        public uint SortOrder { get; set; } = 0;
        public bool IsUpdate => false;
        public bool IsRender => false;
        public bool IsMouse => true;
        public bool IsKeyboard => true;

        private bool HandleWorkPanelKeys(Keyboard keyboard)
        {
            if (_uiStateManager.OpenDrawer != DrawerId.Work)
                return false;

            if (_uiStateManager.DrawerTab == 2)
                return HandleJobAllocationKeys(keyboard);

            return false;
        }

        private bool HandleJobAllocationKeys(Keyboard keyboard)
        {
            var service = _professionAssignmentsProvider();
            if (service == null) return false;
            var defs = service.Registry.Definitions;
            if (defs.Count == 0) return false;

            var roster = _professionRosterProvider();
            if (roster.Count == 0) return false;

            var ui = _uiStateManager.Store;
            ui.WorkAllocSelectedRow = Math.Clamp(ui.WorkAllocSelectedRow, 0, roster.Count - 1);
            ui.WorkAllocSelectedCol = Math.Clamp(ui.WorkAllocSelectedCol, 0, defs.Count - 1);

            bool handled = false;
            if (keyboard.IsKeyPressed(Keys.Up))
            {
                ui.WorkAllocSelectedRow = Math.Max(0, ui.WorkAllocSelectedRow - 1);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.Down))
            {
                ui.WorkAllocSelectedRow = Math.Min(roster.Count - 1, ui.WorkAllocSelectedRow + 1);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.Left))
            {
                ui.WorkAllocSelectedCol = Math.Max(0, ui.WorkAllocSelectedCol - 1);
                handled = true;
            }
            else if (keyboard.IsKeyPressed(Keys.Right))
            {
                ui.WorkAllocSelectedCol = Math.Min(defs.Count - 1, ui.WorkAllocSelectedCol + 1);
                handled = true;
            }

            if (handled)
            {
                int drawerHeight = _screenHeight - 1;
                int maxHeight = drawerHeight - 3;
                int areaHeight = Math.Max(10, maxHeight);
                int visibleRows = Math.Max(1, areaHeight - 4);
                if (ui.WorkAllocSelectedRow < ui.WorkAllocRowOffset)
                    ui.WorkAllocRowOffset = ui.WorkAllocSelectedRow;
                else if (ui.WorkAllocSelectedRow >= ui.WorkAllocRowOffset + visibleRows)
                    ui.WorkAllocRowOffset = Math.Max(0, ui.WorkAllocSelectedRow - visibleRows + 1);
                return true;
            }

            int? weight = GetWeightFromKeyboard(keyboard);
            if (!weight.HasValue) return false;

            var entry = roster[ui.WorkAllocSelectedRow];
            var definition = defs[ui.WorkAllocSelectedCol];
            _setProfessionWeight(entry.WorkerId, definition.Id, weight.Value);
            var label = weight.Value == 0 ? "-" : weight.Value.ToString();
            AddToast($"{entry.Name}: {definition.Name} -> {label}", 60);
            return true;
        }

        private static int? GetWeightFromKeyboard(Keyboard keyboard)
        {
            if (keyboard.IsKeyPressed(Keys.OemMinus) || keyboard.IsKeyPressed(Keys.Subtract))
                return 0;
            if (keyboard.IsKeyPressed(Keys.D1) || keyboard.IsKeyPressed(Keys.NumPad1)) return 1;
            if (keyboard.IsKeyPressed(Keys.D2) || keyboard.IsKeyPressed(Keys.NumPad2)) return 2;
            if (keyboard.IsKeyPressed(Keys.D3) || keyboard.IsKeyPressed(Keys.NumPad3)) return 3;
            if (keyboard.IsKeyPressed(Keys.D4) || keyboard.IsKeyPressed(Keys.NumPad4)) return 4;
            if (keyboard.IsKeyPressed(Keys.D5) || keyboard.IsKeyPressed(Keys.NumPad5)) return 5;
            if (keyboard.IsKeyPressed(Keys.D6) || keyboard.IsKeyPressed(Keys.NumPad6)) return 6;
            if (keyboard.IsKeyPressed(Keys.D7) || keyboard.IsKeyPressed(Keys.NumPad7)) return 7;
            if (keyboard.IsKeyPressed(Keys.D8) || keyboard.IsKeyPressed(Keys.NumPad8)) return 8;
            if (keyboard.IsKeyPressed(Keys.D9) || keyboard.IsKeyPressed(Keys.NumPad9)) return 9;
            return null;
        }

        private bool HandleJobAllocationClick(Point localPos)
        {
            if (_uiStateManager.OpenDrawer != DrawerId.Work || _uiStateManager.DrawerTab != 2)
                return false;

            var service = _professionAssignmentsProvider();
            var world = _worldProvider();
            if (service == null || world == null) return false;

            var defs = service.Registry.Definitions;
            var roster = _professionRosterProvider();
            if (defs.Count == 0 || roster.Count == 0) return false;

            // Match drawer height calculation from UiRenderer.DrawDrawer
            int drawerHeight = Math.Max(10, _screenHeight - 7);
            int drawerTopY = _screenHeight - 1 - drawerHeight;
            int startY = drawerTopY + 1;
            int maxHeight = drawerHeight - 3;
            int areaHeight = Math.Max(10, maxHeight);
            var area = new SadRogue.Primitives.Rectangle(1, startY, _screenWidth - 2, areaHeight);
            if (!area.Contains(localPos)) return false;

            int nameWidth = Math.Max(12, area.Width / 6);
            int tableWidth = Math.Max(8, area.Width - nameWidth - 3);
            int colWidth = Math.Max(3, tableWidth / defs.Count);
            // Note: area.Y is startY which is drawer y0 + 1.
            // Header is at area.Y + 1 (after title line "Job Allocation...")
            // Body rows start at area.Y + 2 (headerY + 1)
            int headerY = area.Y + 1;
            int bodyStartY = headerY + 1;
            if (localPos.Y < bodyStartY) return false;

            int visibleRows = Math.Max(1, area.Height - 4);
            var ui = _uiStateManager.Store;

            int rowIndex = localPos.Y - bodyStartY;
            if (rowIndex < 0 || rowIndex >= visibleRows) return false;
            int actualRow = ui.WorkAllocRowOffset + rowIndex;
            if (actualRow < 0 || actualRow >= roster.Count) return false;

            int nameX = area.X + 1;
            int bodyX = nameX + nameWidth;
            if (localPos.X < nameX) return false;

            ui.WorkAllocSelectedRow = actualRow;

            if (localPos.X < bodyX)
            {
                return true;
            }

            int colIndex = (localPos.X - bodyX) / colWidth;
            colIndex = Math.Min(Math.Max(colIndex, 0), defs.Count - 1);
            ui.WorkAllocSelectedCol = colIndex;

            int offset = Math.Max(0, Math.Min(ui.WorkAllocRowOffset, roster.Count - visibleRows));
            if (actualRow < offset) ui.WorkAllocRowOffset = actualRow;
            else if (actualRow >= offset + visibleRows) ui.WorkAllocRowOffset = Math.Max(0, actualRow - visibleRows + 1);

            var entry = roster[actualRow];
            var definition = defs[colIndex];
            int current = entry.Weights.TryGetValue(definition.Id, out var val) ? val : 5;
            int next = current switch
            {
                <= 0 => 1,
                >= 9 => 0,
                _ => current + 1
            };
            _setProfessionWeight(entry.WorkerId, definition.Id, next);
            string label = next <= 0 ? "-" : next.ToString();
            AddToast($"{definition.Name}: {label}", 60);
            _uiStateManager.Store.SuppressNextTileClick = true;
            return true;
        }
    }
}
