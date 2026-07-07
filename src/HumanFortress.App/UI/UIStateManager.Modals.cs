using HumanFortress.App;

namespace HumanFortress.App.UI;

internal sealed partial class UIStateManager
{
    /// <summary>
    /// Toggle help panel.
    /// </summary>
    public void ToggleHelp()
    {
        Logger.Log("[UIStateManager] ToggleHelp");
        _store.ToggleHelp();
    }

    /// <summary>
    /// Toggle debug panel.
    /// </summary>
    public void ToggleDebug()
    {
        Logger.Log("[UIStateManager] ToggleDebug");
        _store.ToggleDebug();
    }

    /// <summary>
    /// Toggle pause menu.
    /// </summary>
    public void TogglePause()
    {
        Logger.Log("[UIStateManager] TogglePause");
        _store.TogglePause();
    }

    /// <summary>
    /// Add toast notification.
    /// </summary>
    public void AddToast(string text, ulong expireTick)
    {
        _store.AddToast(text, expireTick);
    }

    /// <summary>
    /// Clean up expired toasts.
    /// </summary>
    public void PruneToasts(ulong nowTick)
    {
        _store.PruneToasts(nowTick);
    }
}
