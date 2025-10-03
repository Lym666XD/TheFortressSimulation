using System.Collections.Generic;

namespace HumanFortress.Core.Content;

/// <summary>
/// Content-driven zone definition data (DTO for JSON loading).
/// </summary>
public sealed class ZoneDefinitionData
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ZoneUiHintsData UiHints { get; set; } = new();
    public ZonePoliciesData DefaultPolicies { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}

public sealed class ZoneUiHintsData
{
    public char Glyph { get; set; } = '≡';
    public string Color { get; set; } = "#FFFFFF";
    public string Keybind { get; set; } = string.Empty;
}

public sealed class ZonePoliciesData
{
    public string NavCostMode { get; set; } = "none";
    public List<string> AllowsActions { get; set; } = new();
}
