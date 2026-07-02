namespace HumanFortress.Contracts.Content.Registry;

/// <summary>
/// Static stockpile preset definition loaded from content/registries/stockpile_presets.json.
/// </summary>
public sealed class StockpilePresetDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "Whitelist";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string[] ItemIds { get; set; } = Array.Empty<string>();
    public string[] Materials { get; set; } = Array.Empty<string>();
    public int Priority { get; set; } = 1;
    public string? Description { get; set; }
}
