namespace HumanFortress.App.UI.Commands;

/// <summary>
/// Command to toggle debug panel.
/// </summary>
internal sealed class ToggleDebugCommand : IUICommand
{
    public string CommandType => "ui.toggle_debug";

    public void Execute(UIStateManager uiState)
    {
        uiState.ToggleDebug();
    }
}
