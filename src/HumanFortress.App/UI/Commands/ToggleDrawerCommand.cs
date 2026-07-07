namespace HumanFortress.App.UI.Commands;

/// <summary>
/// Command to toggle drawer panel (F1-F8 buttons)
/// </summary>
internal sealed class ToggleDrawerCommand : IUICommand
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
