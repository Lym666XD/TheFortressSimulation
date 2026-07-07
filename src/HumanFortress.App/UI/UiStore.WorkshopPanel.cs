using System;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    public bool WorkshopPanelOpen { get; private set; } = false;
    public int WorkPanelSelectedIndex { get; set; } = 0;
    public int WorkAllocSelectedRow { get; set; } = 0;
    public int WorkAllocSelectedCol { get; set; } = 0;
    public int WorkAllocRowOffset { get; set; } = 0;
    public bool SuppressNextTileClick { get; set; } = false;
    public Guid? OpenWorkshopGuid { get; private set; } = null;
    public Point OpenWorkshopAnchor { get; private set; } = new(0, 0);
    public int OpenWorkshopZ { get; private set; } = 0;
    public int WorkshopQueueSelectedIndex { get; set; } = 0;
    public int WorkshopQueueScroll { get; set; } = 0;

    public void OpenWorkshopPanel(Guid guid, Point anchor, int z)
    {
        OpenWorkshopGuid = guid;
        OpenWorkshopAnchor = anchor;
        OpenWorkshopZ = z;
        WorkshopPanelOpen = true;
        WorkshopQueueSelectedIndex = 0;
        WorkshopQueueScroll = 0;
    }

    public void CloseWorkshopPanel()
    {
        WorkshopPanelOpen = false;
        OpenWorkshopGuid = null;
        WorkshopQueueSelectedIndex = 0;
        WorkshopQueueScroll = 0;
    }
}
