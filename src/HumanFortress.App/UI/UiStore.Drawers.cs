namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    public void OpenPanel(DrawerId id)
    {
        if (OpenDrawer == id)
        {
            OpenDrawer = DrawerId.None;
            Context = QuickMenu != QuickMenuKind.None ? UiContext.QuickMenu : UiContext.Global;
            return;
        }

        OpenDrawer = id;
        DrawerTab = 0;
        if (id == DrawerId.Work)
        {
            WorkPanelSelectedIndex = 0;
            WorkAllocSelectedRow = 0;
            WorkAllocSelectedCol = 0;
            WorkAllocRowOffset = 0;
        }

        Context = UiContext.Drawer;
    }

    public void TabNext()
    {
        if (Context != UiContext.Drawer) return;
        int count = GetDrawerTabCount();
        if (count <= 0) return;
        DrawerTab = (DrawerTab + 1) % count;
    }

    public void TabPrev()
    {
        if (Context != UiContext.Drawer) return;
        int count = GetDrawerTabCount();
        if (count <= 0) return;
        DrawerTab = (DrawerTab - 1 + count) % count;
    }

    public void SetDrawerTab(int index)
    {
        if (Context != UiContext.Drawer) return;
        int count = GetDrawerTabCount();
        if (count <= 0) return;
        DrawerTab = Math.Clamp(index, 0, count - 1);
    }

    private int GetDrawerTabCount()
    {
        return OpenDrawer switch
        {
            DrawerId.Work => 5,
            DrawerId.Creature => 3,
            DrawerId.Stock => 3,
            DrawerId.PlacementManagement => 3,
            _ => 3
        };
    }
}
