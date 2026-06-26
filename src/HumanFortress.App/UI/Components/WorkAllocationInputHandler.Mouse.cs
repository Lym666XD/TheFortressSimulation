using SadRogue.Primitives;

namespace HumanFortress.App.UI.Components;

internal sealed partial class WorkAllocationInputHandler
{
    public bool HandleClick(Point localPos)
    {
        if (_uiStateManager.OpenDrawer != DrawerId.Work || _uiStateManager.DrawerTab != 2)
            return false;

        var workforce = _workforceProvider();
        var defs = workforce.Professions;
        var roster = workforce.Roster;
        if (defs.Count == 0 || roster.Count == 0) return false;

        var area = CalculateJobAllocationArea();
        if (!area.Contains(localPos)) return false;

        int nameWidth = Math.Max(12, area.Width / 6);
        int tableWidth = Math.Max(8, area.Width - nameWidth - 3);
        int colWidth = Math.Max(3, tableWidth / defs.Count);
        int headerY = area.Y + 1;
        int bodyStartY = headerY + 1;
        if (localPos.Y < bodyStartY) return false;

        int visibleRows = Math.Max(1, area.Height - 4);
        var ui = _uiStateManager.Store;

        int rowIndex = localPos.Y - bodyStartY;
        if (rowIndex < 0 || rowIndex >= visibleRows) return false;
        int actualRow = ui.WorkAllocRowOffset + rowIndex;
        if (actualRow < 0 || actualRow >= roster.Count) return false;

        int nameX = area.X + 1;
        int bodyX = nameX + nameWidth;
        if (localPos.X < nameX) return false;

        ui.WorkAllocSelectedRow = actualRow;

        if (localPos.X < bodyX)
        {
            return true;
        }

        int colIndex = (localPos.X - bodyX) / colWidth;
        colIndex = Math.Min(Math.Max(colIndex, 0), defs.Count - 1);
        ui.WorkAllocSelectedCol = colIndex;

        int offset = Math.Max(0, Math.Min(ui.WorkAllocRowOffset, roster.Count - visibleRows));
        if (actualRow < offset) ui.WorkAllocRowOffset = actualRow;
        else if (actualRow >= offset + visibleRows) ui.WorkAllocRowOffset = Math.Max(0, actualRow - visibleRows + 1);

        var entry = roster[actualRow];
        var definition = defs[colIndex];
        int current = entry.Weights.TryGetValue(definition.Id, out var val) ? val : 5;
        int next = current switch
        {
            <= 0 => 1,
            >= 9 => 0,
            _ => current + 1
        };
        _setProfessionWeight(entry.WorkerId, definition.Id, next);
        string label = next <= 0 ? "-" : next.ToString();
        _addToast($"{definition.Name}: {label}", 60);
        _uiStateManager.Store.SuppressNextTileClick = true;
        return true;
    }
}
