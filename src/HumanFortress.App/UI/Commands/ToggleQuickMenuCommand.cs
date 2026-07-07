namespace HumanFortress.App.UI.Commands;

/// <summary>
/// Command to toggle quick menu (Z/X/C/V buttons)
/// </summary>
internal sealed class ToggleQuickMenuCommand : IUICommand
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
