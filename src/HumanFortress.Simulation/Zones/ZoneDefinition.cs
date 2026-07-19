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
    internal string Id { get; set; } = string.Empty;

    /// <summary>
    /// Category: production, civil, public, military, management.
    /// </summary>
    internal string Category { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI.
    /// </summary>
    internal string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// UI hints for rendering.
    /// </summary>
    internal ZoneUiHints UiHints { get; set; } = new();

    /// <summary>
    /// Default policies for this zone type.
    /// </summary>
    internal ZonePolicies DefaultPolicies { get; set; } = new();

    /// <summary>
    /// Optional notes/description.
    /// </summary>
    internal string Notes { get; set; } = string.Empty;
}

/// <summary>
/// UI hints for zone visualization.
/// </summary>
internal sealed class ZoneUiHints
{
    /// <summary>
    /// Glyph character for display.
    /// </summary>
    internal char Glyph { get; set; } = '≡';

    /// <summary>
    /// Color in hex format (e.g., "#FFD700").
    /// </summary>
    internal string Color { get; set; } = "#FFFFFF";

    /// <summary>
    /// Keybind hint (e.g., "Z", "X").
    /// </summary>
    internal string Keybind { get; set; } = string.Empty;
}

/// <summary>
/// Zone policies (defaults from definition, can be overridden per instance).
/// </summary>
internal sealed class ZonePolicies
{
    /// <summary>
    /// Navigation cost mode: "none" or "restricted".
    /// </summary>
    internal string NavCostMode { get; set; } = "none";

    /// <summary>
    /// List of allowed action types (future use).
    /// </summary>
    internal List<string> AllowsActions { get; set; } = new();
}
