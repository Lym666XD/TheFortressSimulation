namespace HumanFortress.App.UI.Commands
{
    /// <summary>
    /// Command to toggle drawer panel (F1-F8 buttons)
    /// </summary>
    public sealed class ToggleDrawerCommand : IUICommand
    {
        public string CommandType => "ui.toggle_drawer";

        private readonly DrawerId _drawerId;

        public ToggleDrawerCommand(DrawerId drawerId)
        {
            _drawerId = drawerId;
        }

        public void Execute(UIStateManager uiState)
        {
            uiState.ToggleDrawer(_drawerId);
        }
    }

    /// <summary>
    /// Command to toggle quick menu (Z/X/C/V buttons)
    /// </summary>
    public sealed class ToggleQuickMenuCommand : IUICommand
    {
        public string CommandType => "ui.toggle_quick_menu";

        private readonly QuickMenuKind _kind;

        public ToggleQuickMenuCommand(QuickMenuKind kind)
        {
            _kind = kind;
        }

        public void Execute(UIStateManager uiState)
        {
            uiState.ToggleQuickMenu(_kind);
        }
    }

    /// <summary>
    /// Command to open submenu (L2 → L3 navigation)
    /// </summary>
    public sealed class OpenSubmenuCommand : IUICommand
    {
        public string CommandType => "ui.open_submenu";

        private readonly int _submenuIndex;

        public OpenSubmenuCommand(int submenuIndex)
        {
            _submenuIndex = submenuIndex;
        }

        public void Execute(UIStateManager uiState)
        {
            uiState.OpenSubmenu(_submenuIndex);
        }
    }

    /// <summary>
    /// Command to navigate back in UI hierarchy (hierarchical back)
    /// </summary>
    public sealed class NavigateBackCommand : IUICommand
    {
        public string CommandType => "ui.navigate_back";

        public void Execute(UIStateManager uiState)
        {
            uiState.NavigateBack();
        }
    }

    /// <summary>
    /// Command to cancel current operation (ESC / Right-click)
    /// </summary>
    public sealed class CancelCommand : IUICommand
    {
        public string CommandType => "ui.cancel";

        public void Execute(UIStateManager uiState)
        {
            uiState.Cancel();
        }
    }

    /// <summary>
    /// Command to switch drawer tab
    /// </summary>
    public sealed class SwitchDrawerTabCommand : IUICommand
    {
        public string CommandType => "ui.switch_drawer_tab";

        private readonly int _tabIndex;

        public SwitchDrawerTabCommand(int tabIndex)
        {
            _tabIndex = tabIndex;
        }

        public void Execute(UIStateManager uiState)
        {
            uiState.SwitchDrawerTab(_tabIndex);
        }
    }

    /// <summary>
    /// Command to toggle help panel
    /// </summary>
    public sealed class ToggleHelpCommand : IUICommand
    {
        public string CommandType => "ui.toggle_help";

        public void Execute(UIStateManager uiState)
        {
            uiState.ToggleHelp();
        }
    }

    /// <summary>
    /// Command to toggle debug panel
    /// </summary>
    public sealed class ToggleDebugCommand : IUICommand
    {
        public string CommandType => "ui.toggle_debug";

        public void Execute(UIStateManager uiState)
        {
            uiState.ToggleDebug();
        }
    }

    /// <summary>
    /// Command to toggle pause menu
    /// </summary>
    public sealed class TogglePauseCommand : IUICommand
    {
        public string CommandType => "ui.toggle_pause";

        public void Execute(UIStateManager uiState)
        {
            uiState.TogglePause();
        }
    }
}
