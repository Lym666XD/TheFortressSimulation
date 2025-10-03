using SadConsole;
using SadConsole.Components;
using SadConsole.Input;
using SadRogue.Primitives;
using HumanFortress.App.UI.Commands;

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
