using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class WorkAllocationInputHandler
{
    private void ScrollSelectionIntoView(UiStore ui)
    {
        int drawerHeight = _screenHeight - 1;
        int maxHeight = drawerHeight - 3;
        int areaHeight = Math.Max(10, maxHeight);
        int visibleRows = Math.Max(1, areaHeight - 4);
        if (ui.WorkAllocSelectedRow < ui.WorkAllocRowOffset)
            ui.WorkAllocRowOffset = ui.WorkAllocSelectedRow;
        else if (ui.WorkAllocSelectedRow >= ui.WorkAllocRowOffset + visibleRows)
            ui.WorkAllocRowOffset = Math.Max(0, ui.WorkAllocSelectedRow - visibleRows + 1);
    }

    private Rectangle CalculateJobAllocationArea()
    {
        int drawerHeight = Math.Max(10, _screenHeight - 7);
        int drawerTopY = _screenHeight - 1 - drawerHeight;
        int startY = drawerTopY + 1;
        int maxHeight = drawerHeight - 3;
        int areaHeight = Math.Max(10, maxHeight);
        return new Rectangle(1, startY, _screenWidth - 2, areaHeight);
    }
}
