namespace HumanFortress.App.UI.Commands;

/// <summary>
/// Command to open submenu.
/// </summary>
internal sealed class OpenSubmenuCommand : IUICommand
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
