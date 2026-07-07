using SadConsole.Input;

namespace HumanFortress.App.UI.Components;

internal sealed partial class WorkAllocationInputHandler
{
    public bool HandleKeyboard(Keyboard keyboard)
    {
        if (_uiStateManager.OpenDrawer != DrawerId.Work || _uiStateManager.DrawerTab != 2)
        {
            return false;
        }

        var workforce = _workforceProvider();
        var defs = workforce.Professions;
        if (defs.Count == 0) return false;

        var roster = workforce.Roster;
        if (roster.Count == 0) return false;

        var ui = _uiStateManager.Store;
        ui.WorkAllocSelectedRow = Math.Clamp(ui.WorkAllocSelectedRow, 0, roster.Count - 1);
        ui.WorkAllocSelectedCol = Math.Clamp(ui.WorkAllocSelectedCol, 0, defs.Count - 1);

        bool handled = false;
        if (keyboard.IsKeyPressed(Keys.Up))
        {
            ui.WorkAllocSelectedRow = Math.Max(0, ui.WorkAllocSelectedRow - 1);
            handled = true;
        }
        else if (keyboard.IsKeyPressed(Keys.Down))
        {
            ui.WorkAllocSelectedRow = Math.Min(roster.Count - 1, ui.WorkAllocSelectedRow + 1);
            handled = true;
        }
        else if (keyboard.IsKeyPressed(Keys.Left))
        {
            ui.WorkAllocSelectedCol = Math.Max(0, ui.WorkAllocSelectedCol - 1);
            handled = true;
        }
        else if (keyboard.IsKeyPressed(Keys.Right))
        {
            ui.WorkAllocSelectedCol = Math.Min(defs.Count - 1, ui.WorkAllocSelectedCol + 1);
            handled = true;
        }

        if (handled)
        {
            ScrollSelectionIntoView(ui);
            return true;
        }

        int? weight = GetWeightFromKeyboard(keyboard);
        if (!weight.HasValue) return false;

        var entry = roster[ui.WorkAllocSelectedRow];
        var definition = defs[ui.WorkAllocSelectedCol];
        _setProfessionWeight(entry.WorkerId, definition.Id, weight.Value);
        var label = weight.Value == 0 ? "-" : weight.Value.ToString();
        _addToast($"{entry.Name}: {definition.Name} -> {label}", 60);
        return true;
    }

    private static int? GetWeightFromKeyboard(Keyboard keyboard)
    {
        if (keyboard.IsKeyPressed(Keys.OemMinus) || keyboard.IsKeyPressed(Keys.Subtract))
            return 0;
        if (keyboard.IsKeyPressed(Keys.D1) || keyboard.IsKeyPressed(Keys.NumPad1)) return 1;
        if (keyboard.IsKeyPressed(Keys.D2) || keyboard.IsKeyPressed(Keys.NumPad2)) return 2;
        if (keyboard.IsKeyPressed(Keys.D3) || keyboard.IsKeyPressed(Keys.NumPad3)) return 3;
        if (keyboard.IsKeyPressed(Keys.D4) || keyboard.IsKeyPressed(Keys.NumPad4)) return 4;
        if (keyboard.IsKeyPressed(Keys.D5) || keyboard.IsKeyPressed(Keys.NumPad5)) return 5;
        if (keyboard.IsKeyPressed(Keys.D6) || keyboard.IsKeyPressed(Keys.NumPad6)) return 6;
        if (keyboard.IsKeyPressed(Keys.D7) || keyboard.IsKeyPressed(Keys.NumPad7)) return 7;
        if (keyboard.IsKeyPressed(Keys.D8) || keyboard.IsKeyPressed(Keys.NumPad8)) return 8;
        if (keyboard.IsKeyPressed(Keys.D9) || keyboard.IsKeyPressed(Keys.NumPad9)) return 9;
        return null;
    }
}
