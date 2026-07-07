namespace HumanFortress.App.UI.Commands;

/// <summary>
/// Command to toggle pause menu.
/// </summary>
internal sealed class TogglePauseCommand : IUICommand
{
    public string CommandType => "ui.toggle_pause";

    public void Execute(UIStateManager uiState)
    {
        uiState.TogglePause();
    }
}
