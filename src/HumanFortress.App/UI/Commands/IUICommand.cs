namespace HumanFortress.App.UI.Commands
{
    /// <summary>
    /// Interface for UI-only commands that modify UI state.
    /// Unlike ICommand (simulation commands), these execute immediately and don't go through CommandQueue.
    /// </summary>
    public interface IUICommand
    {
        /// <summary>
        /// Execute the UI command, modifying UI state directly.
        /// </summary>
        void Execute(UIStateManager uiState);

        /// <summary>
        /// Command type for logging and debugging.
        /// </summary>
        string CommandType { get; }
    }
}
