namespace HumanFortress.App.UI.Commands;

/// <summary>
/// Command to toggle help panel.
/// </summary>
internal sealed class ToggleHelpCommand : IUICommand
{
    public string CommandType => "ui.toggle_help";

    public void Execute(UIStateManager uiState)
    {
        uiState.ToggleHelp();
    }
}
