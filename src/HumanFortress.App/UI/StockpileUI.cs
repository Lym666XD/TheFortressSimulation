using SadRogue.Primitives;

namespace HumanFortress.App.UI;

/// <summary>
/// UI handler for stockpile zones.
/// Manages creation, editing, deletion, and visualization.
/// </summary>
internal sealed partial class StockpileUI
{
    private StockpilePresetOption[]? _presets;
    private int _selectedPresetIndex = 0;

    private bool _editPopupOpen = false;
    private int? _editingZoneId = null;
    private Point _editPopupPos;

    public StockpileUI()
    {
        LoadPresets();
    }

    public int? EditingZoneId => _editingZoneId;

    private void LoadPresets()
    {
        // TODO: Load from stockpile_presets.json.
        _presets = new[]
        {
            new StockpilePresetOption("all", "All Items"),
            new StockpilePresetOption("wood", "Wood"),
            new StockpilePresetOption("stone", "Stone"),
            new StockpilePresetOption("metal", "Metal"),
            new StockpilePresetOption("food", "Food"),
            new StockpilePresetOption("refuse", "Refuse")
        };
    }

    private readonly record struct StockpilePresetOption(string Id, string Name);
}
