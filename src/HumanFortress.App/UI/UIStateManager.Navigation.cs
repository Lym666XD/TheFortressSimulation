using HumanFortress.App;

namespace HumanFortress.App.UI;

internal sealed partial class UIStateManager
{
    /// <summary>
    /// Toggle drawer panel (F1-F8 buttons).
    /// </summary>
    public void ToggleDrawer(DrawerId drawerId)
    {
        if (drawerId == DrawerId.None) return;

        Logger.Log($"[UIStateManager] ToggleDrawer: {drawerId}");
        _store.OpenPanel(drawerId);
    }

    /// <summary>
    /// Toggle quick menu (Z/X/C/V buttons).
    /// </summary>
    public void ToggleQuickMenu(QuickMenuKind kind)
    {
        if (kind == QuickMenuKind.None) return;

        Logger.Log($"[UIStateManager] ToggleQuickMenu: {kind}");
        _store.OpenQuickMenu(kind);
    }

    /// <summary>
    /// Navigate back in UI hierarchy.
    /// </summary>
    public void NavigateBack()
    {
        Logger.Log($"[UIStateManager] NavigateBack: context={_store.Context}");
        _store.Back();
    }

    /// <summary>
    /// Cancel current operation (ESC / right click).
    /// </summary>
    public void Cancel()
    {
        Logger.Log($"[UIStateManager] Cancel: context={_store.Context}");
        _store.Cancel();
    }

}
