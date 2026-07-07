namespace HumanFortress.App.UI.Commands;

/// <summary>
/// Command to navigate back in UI hierarchy.
/// </summary>
internal sealed class NavigateBackCommand : IUICommand
{
    public string CommandType => "ui.navigate_back";

    public void Execute(UIStateManager uiState)
    {
        uiState.NavigateBack();
    }
}
