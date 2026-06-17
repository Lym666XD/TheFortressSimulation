using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HumanFortress.Core.Content;

/// <summary>
/// Content-driven zone definition data loaded from JSON.
/// </summary>
public sealed class ZoneDefinitionData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("ui_hints")]
    public ZoneUiHintsData UiHints { get; set; } = new();

    [JsonPropertyName("default_policies")]
    public ZonePoliciesData DefaultPolicies { get; set; } = new();

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public sealed class ZoneUiHintsData
{
    [JsonPropertyName("glyph")]
    public char Glyph { get; set; } = '\u2261';

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#FFFFFF";

    [JsonPropertyName("keybind")]
    public string Keybind { get; set; } = string.Empty;
}

public sealed class ZonePoliciesData
{
    [JsonPropertyName("nav_cost_mode")]
    public string NavCostMode { get; set; } = "none";

    [JsonPropertyName("allows_actions")]
    public List<string> AllowsActions { get; set; } = new();
}
