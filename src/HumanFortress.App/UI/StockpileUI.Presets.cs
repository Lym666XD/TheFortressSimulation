using SadConsole;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal sealed partial class StockpileUI
{
    public void ApplyPresetMenu(SimulationStockpilePresetMenuData menu)
    {
        var options = menu.Options ?? Array.Empty<StockpilePresetMenuOptionView>();
        _presets = options.Count == 0
            ? DefaultPresets
            : options
                .Take(9)
                .Select(static option => new StockpilePresetOption(option.Id, option.Name))
                .ToArray();

        if (_selectedPresetIndex >= _presets.Length)
            _selectedPresetIndex = Math.Max(0, _presets.Length - 1);
    }

    /// <summary>
    /// Handle preset selection input.
    /// </summary>
    public string? HandlePresetSelection(int key)
    {
        if (key < 1 || key > 9)
            return null;

        int index = key - 1;
        if (index < _presets.Length)
        {
            _selectedPresetIndex = index;
            return _presets[index].Id;
        }

        return null;
    }

    /// <summary>
    /// Draw preset selection menu.
    /// </summary>
    private void DrawPresetSelection(ScreenSurface surface)
    {
        if (_presets.Length == 0)
            return;

        int x = surface.Width / 2 - 15;
        int y = surface.Height / 2 - 8;

        var bg = Color.Black.SetAlpha(220);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 30, 12, fg, bg);
        surface.Print(x + 1, y, " SELECT PRESET ", highlight);

        for (int i = 0; i < Math.Min(_presets.Length, 9); i++)
        {
            var preset = _presets[i];
            var key = (i + 1).ToString();
            var selected = i == _selectedPresetIndex;

            var lineColor = selected ? highlight : fg;
            surface.Print(x + 2, y + 2 + i, $"[{key}] {preset.Name}", lineColor);
        }

        surface.Print(x + 2, y + 11, "Enter/Click to confirm", Color.Gray);
    }
}
