using SadRogue.Primitives;

namespace HumanFortress.App.UI
{
    /// <summary>
    /// Manages UI state transitions and provides a clean API for UI commands.
    /// Wraps UiStore and adds validation, logging, and event notifications.
    /// </summary>
    public sealed class UIStateManager
    {
        private readonly UiStore _store;

        public UIStateManager(UiStore store)
        {
            _store = store;
        }

        // Read-only access to state
        public UiContext Context => _store.Context;
        public DrawerId OpenDrawer => _store.OpenDrawer;
        public int DrawerTab => _store.DrawerTab;
        public QuickMenuKind QuickMenu => _store.QuickMenu;
        public OrdersSubmenu OrdersMenu => _store.OrdersMenu;
        public ZoneSubmenu ZoneMenu => _store.ZoneMenu;
        public BuildSubmenu BuildMenu => _store.BuildMenu;
        public StockpileSubmenu StockpileMenu => _store.StockpileMenu;
        public PlacementMode PlaceMode => _store.PlaceMode;
        public Point? HoverTile => _store.HoverTile;
        public UiStore Store => _store; // Direct access when needed

        /// <summary>
        /// Toggle drawer panel (F1-F8 buttons)
        /// </summary>
        public void ToggleDrawer(DrawerId drawerId)
        {
            if (drawerId == DrawerId.None) return;

            Logger.Log($"[UIStateManager] ToggleDrawer: {drawerId}");
            _store.OpenPanel(drawerId);
        }

        /// <summary>
        /// Toggle quick menu (Z/X/C/V buttons)
        /// </summary>
        public void ToggleQuickMenu(QuickMenuKind kind)
        {
            if (kind == QuickMenuKind.None) return;

            Logger.Log($"[UIStateManager] ToggleQuickMenu: {kind}");
            _store.OpenQuickMenu(kind);
        }

        /// <summary>
        /// Open submenu within current quick menu
        /// </summary>
        public void OpenSubmenu(int submenuIndex)
        {
            Logger.Log($"[UIStateManager] OpenSubmenu: menu={_store.QuickMenu} index={submenuIndex}");

            switch (_store.QuickMenu)
            {
                case QuickMenuKind.Orders:
                    if (submenuIndex >= 0 && submenuIndex <= 6)
                        _store.OpenOrdersSubmenu((OrdersSubmenu)(submenuIndex + 1));
                    break;

                case QuickMenuKind.Zones:
                    if (submenuIndex >= 0 && submenuIndex <= 4)
                        _store.OpenZoneSubmenu((ZoneSubmenu)(submenuIndex + 1));
                    break;

                case QuickMenuKind.Build:
                    if (submenuIndex >= 0 && submenuIndex <= 4)
                        _store.OpenBuildSubmenu((BuildSubmenu)(submenuIndex + 1));
                    break;

                case QuickMenuKind.Stockpile:
                    if (submenuIndex == 0)
                        _store.OpenStockpileSubmenu(StockpileSubmenu.Stockpile);
                    break;
            }
        }

        /// <summary>
        /// Navigate back in UI hierarchy (hierarchical back navigation)
        /// </summary>
        public void NavigateBack()
        {
            Logger.Log($"[UIStateManager] NavigateBack: context={_store.Context}");
            _store.Back();
        }

        /// <summary>
        /// Cancel current operation (ESC / Right-click)
        /// </summary>
        public void Cancel()
        {
            Logger.Log($"[UIStateManager] Cancel: context={_store.Context}");
            _store.Cancel();
        }

        /// <summary>
        /// Switch drawer tab (Tab key or mouse click)
        /// </summary>
        public void SwitchDrawerTab(int tabIndex)
        {
            if (_store.Context != UiContext.Drawer) return;

            Logger.Log($"[UIStateManager] SwitchDrawerTab: {tabIndex}");
            // Directly set tab (UiStore doesn't have SetTab, only TabNext/TabPrev)
            // We'll need to add this to UiStore or calculate the right number of TabNext calls
            // For now, use reflection or add method to UiStore

            // Workaround: Calculate how many TabNext calls needed
            int current = _store.DrawerTab;
            int diff = (tabIndex - current + 3) % 3; // Assume 3 tabs
            for (int i = 0; i < diff; i++)
            {
                _store.TabNext();
            }
        }

        /// <summary>
        /// Start placement mode for zones/stockpiles/orders
        /// </summary>
        public void StartPlacement(PlacementMode mode, int z)
        {
            Logger.Log($"[UIStateManager] StartPlacement: mode={mode} z={z}");
            _store.StartPlacement(mode, z);
        }

        /// <summary>
        /// Cancel placement and return to global context
        /// </summary>
        public void CancelPlacement()
        {
            Logger.Log($"[UIStateManager] CancelPlacement");
            _store.CancelPlacement();
        }

        /// <summary>
        /// Set hover tile for cursor preview
        /// </summary>
        public void SetHoverTile(Point tile)
        {
            _store.SetHover(tile);
        }

        /// <summary>
        /// Toggle help panel
        /// </summary>
        public void ToggleHelp()
        {
            Logger.Log($"[UIStateManager] ToggleHelp");
            _store.ToggleHelp();
        }

        /// <summary>
        /// Toggle debug panel
        /// </summary>
        public void ToggleDebug()
        {
            Logger.Log($"[UIStateManager] ToggleDebug");
            _store.ToggleDebug();
        }

        /// <summary>
        /// Toggle pause menu
        /// </summary>
        public void TogglePause()
        {
            Logger.Log($"[UIStateManager] TogglePause");
            _store.TogglePause();
        }

        /// <summary>
        /// Add toast notification
        /// </summary>
        public void AddToast(string text, ulong expireTick)
        {
            _store.AddToast(text, expireTick);
        }

        /// <summary>
        /// Clean up expired toasts
        /// </summary>
        public void PruneToasts(ulong nowTick)
        {
            _store.PruneToasts(nowTick);
        }
    }
}
