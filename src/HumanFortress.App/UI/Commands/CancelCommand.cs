namespace HumanFortress.App.UI.Commands;

/// <summary>
/// Command to cancel current operation.
/// </summary>
internal sealed class CancelCommand : IUICommand
{
    public string CommandType => "ui.cancel";

    public void Execute(UIStateManager uiState)
    {
        uiState.Cancel();
    }
}
