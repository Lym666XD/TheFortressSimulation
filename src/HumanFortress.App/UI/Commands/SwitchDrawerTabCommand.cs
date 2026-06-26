namespace HumanFortress.App.UI.Commands;

/// <summary>
/// Command to switch drawer tab.
/// </summary>
internal sealed class SwitchDrawerTabCommand : IUICommand
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
