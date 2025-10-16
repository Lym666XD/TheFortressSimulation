using SadConsole;
using SadConsole.Components;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.App.UI.Commands;
using HumanFortress.App.UI;
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

        public InputHandlerComponent(UIStateManager uiStateManager, int screenWidth, int screenHeight)
        {
            _uiStateManager = uiStateManager;
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
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
            // Hit-test dock buttons (F1-F8)
            int? dockSlot = ButtonLayoutCalculator.HitTestDockButtons(localPos, _screenWidth, _screenHeight);
            if (dockSlot.HasValue && dockSlot.Value < DockButtonDrawers.Length)
            {
                var drawerId = DockButtonDrawers[dockSlot.Value];
                new ToggleDrawerCommand(drawerId).Execute(_uiStateManager);
                Logger.Log($"[InputHandler] Dock button {dockSlot.Value} -> {drawerId}");
                return true;
            }

            // Hit-test quick buttons (Z/X/C/V)
            int? quickSlot = ButtonLayoutCalculator.HitTestQuickButtons(localPos, _screenWidth, _screenHeight);
            if (quickSlot.HasValue && quickSlot.Value < QuickButtonMenus.Length)
            {
                var menuKind = QuickButtonMenus[quickSlot.Value];
                new ToggleQuickMenuCommand(menuKind).Execute(_uiStateManager);
                Logger.Log($"[InputHandler] Quick button {quickSlot.Value} -> {menuKind}");
                return true;
            }

            // Hit-test F2 Items tab filter pills (when F2 Stock drawer is open on Items tab)
            if (_uiStateManager.OpenDrawer == DrawerId.Stock && _uiStateManager.DrawerTab == 0)
            {
                // Calculate drawer geometry (matches UiRenderer.DrawDrawer)
                int drawerHeight = System.Math.Max(8, (int)(_screenHeight * 0.7));
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
                    var tick = HumanFortress.App.GameStates.GameStateManager.Instance.TickScheduler.CurrentTick;
                    _uiStateManager.AddToast($"Filter: {newFilter}", tick + 50);
                    return true;
                }
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
            bool hasOpenUI = _uiStateManager.OpenDrawer != DrawerId.None
                          || _uiStateManager.QuickMenu != QuickMenuKind.None
                          || _uiStateManager.PlaceMode != PlacementMode.None;

            if (hasOpenUI)
            {
                new NavigateBackCommand().Execute(_uiStateManager);
                Logger.Log($"[InputHandler] Right-click -> NavigateBack (UI open)");
                return true; // Consumed by UI
            }

            // No UI open, let FortressState handle it (tile panel, etc.)
            return false;
        }

        /// <summary>
        /// Handle Debug overlay mouse clicks (category pills & item rows) when Debug panel is open.
        /// Returns true if the click is consumed by the UI.
        /// </summary>
        private bool TryHandleDebugClick(Point localPos)
        {
            var ui = _uiStateManager.Store;
            var worldRef = HumanFortress.App.GameStates.GameStateManager.Instance.World;
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
                    var tick = HumanFortress.App.GameStates.GameStateManager.Instance.TickScheduler.CurrentTick;
                    _uiStateManager.AddToast($"Tab: {(i==0?"Status": i==1?"Creatures":"Items")}", tick + 50);
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
                        var tick = HumanFortress.App.GameStates.GameStateManager.Instance.TickScheduler.CurrentTick;
                        _uiStateManager.AddToast($"Creature: {cLabels[i]}", tick + 50);
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
                    var world = HumanFortress.App.GameStates.GameStateManager.Instance.World;
                    var ids = GetCategoryItemIds(world, newCat).ToList();
                    if (ids.Count > 0) ui.DebugSelectedItem = ids[0];

                    // Visual feedback via toast (flash-like hint)
                    var tick = HumanFortress.App.GameStates.GameStateManager.Instance.TickScheduler.CurrentTick;
                    _uiStateManager.AddToast($"Category: {labels[i]}", tick + 50);
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
                var tick = HumanFortress.App.GameStates.GameStateManager.Instance.TickScheduler.CurrentTick;
                _uiStateManager.AddToast($"Page {ui.DebugItemPage + 1}/{maxPage + 1}", tick + 50);
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
                    var tick = HumanFortress.App.GameStates.GameStateManager.Instance.TickScheduler.CurrentTick;
                    _uiStateManager.AddToast($"Selected: {list[i]}", tick + 40);
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
    }
}





