using System.Collections.Generic;

namespace HumanFortress.Simulation.Zones;

/// <summary>
/// Content-driven zone definition loaded from zones.json per ZONE_SPEC.md §4.1.
/// Immutable template for creating zone instances.
/// </summary>
internal sealed class ZoneDefinition
{
    /// <summary>
    /// Unique identifier (e.g., "bedroom", "assembly", "lumbering").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Category: production, civil, public, military, management.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// UI hints for rendering.
    /// </summary>
    public ZoneUiHints UiHints { get; set; } = new();

    /// <summary>
    /// Default policies for this zone type.
    /// </summary>
    public ZonePolicies DefaultPolicies { get; set; } = new();

    /// <summary>
    /// Optional notes/description.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// UI hints for zone visualization.
/// </summary>
internal sealed class ZoneUiHints
{
    /// <summary>
    /// Glyph character for display.
    /// </summary>
    public char Glyph { get; set; } = '≡';

    /// <summary>
    /// Color in hex format (e.g., "#FFD700").
    /// </summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>
    /// Keybind hint (e.g., "Z", "X").
    /// </summary>
    public string Keybind { get; set; } = string.Empty;
}

/// <summary>
/// Zone policies (defaults from definition, can be overridden per instance).
/// </summary>
internal sealed class ZonePolicies
{
    /// <summary>
    /// Navigation cost mode: "none" or "restricted".
    /// </summary>
    public string NavCostMode { get; set; } = "none";

    /// <summary>
    /// List of allowed action types (future use).
    /// </summary>
    public List<string> AllowsActions { get; set; } = new();
}
