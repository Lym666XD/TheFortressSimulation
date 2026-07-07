using SadRogue.Primitives;

namespace HumanFortress.App.UI;

/// <summary>
/// UI handler for stockpile zones.
/// Manages creation, editing, deletion, and visualization.
/// </summary>
internal sealed partial class StockpileUI
{
    private StockpilePresetOption[] _presets = DefaultPresets;
    private int _selectedPresetIndex = 0;

    private bool _editPopupOpen = false;
    private int? _editingZoneId = null;
    private Point _editPopupPos;

    public int? EditingZoneId => _editingZoneId;

    private static StockpilePresetOption[] DefaultPresets { get; } =
    {
        new("all", "All")
    };

    private readonly record struct StockpilePresetOption(string Id, string Name);
}
