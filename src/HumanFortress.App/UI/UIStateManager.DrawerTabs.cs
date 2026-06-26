using HumanFortress.App;

namespace HumanFortress.App.UI;

internal sealed partial class UIStateManager
{
    /// <summary>
    /// Switch drawer tab.
    /// </summary>
    public void SwitchDrawerTab(int tabIndex)
    {
        if (_store.Context != UiContext.Drawer)
            return;

        Logger.Log($"[UIStateManager] SwitchDrawerTab: {tabIndex}");
        int current = _store.DrawerTab;
        int diff = (tabIndex - current + 3) % 3;
        for (int i = 0; i < diff; i++)
            _store.TabNext();
    }
}
