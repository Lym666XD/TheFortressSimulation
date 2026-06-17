using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HumanFortress.Core.Content;

/// <summary>
/// Runtime geology/terrain data loaded from content JSON.
/// </summary>
public class GeologyData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("material")]
    public string Material { get; set; } = string.Empty;

    [JsonPropertyName("terrain_bits")]
    public TerrainBitsData TerrainBits { get; set; } = new();

    [JsonPropertyName("display")]
    public DisplayData Display { get; set; } = new();

    [JsonPropertyName("properties")]
    public PropertiesData Properties { get; set; } = new();
}

public class TerrainBitsData
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "OpenNoFloor";

    [JsonPropertyName("natural")]
    public bool Natural { get; set; } = true;

    [JsonPropertyName("support_flag")]
    public bool SupportFlag { get; set; }

    [JsonPropertyName("ramp_dir")]
    public string? RampDir { get; set; }

    [JsonPropertyName("rampDirection")]
    public int? RampDirection { get; set; }
}

public class DisplayData
{
    [JsonPropertyName("glyph")]
    public int Glyph { get; set; }

    [JsonPropertyName("foreground")]
    public ColorData Foreground { get; set; } = new();

    [JsonPropertyName("background")]
    public ColorData Background { get; set; } = new();

    [JsonPropertyName("autotile")]
    public AutotileData? Autotile { get; set; }
}

public class ColorData
{
    [JsonPropertyName("r")]
    public int R { get; set; }

    [JsonPropertyName("g")]
    public int G { get; set; }

    [JsonPropertyName("b")]
    public int B { get; set; }
}

public class AutotileData
{
    [JsonPropertyName("connect_groups")]
    public IList<string>? ConnectGroups { get; set; }

    [JsonPropertyName("connects_to")]
    public IList<string>? ConnectsTo { get; set; }

    [JsonPropertyName("variants")]
    public IDictionary<string, int>? Variants { get; set; }
}

public class PropertiesData
{
    [JsonPropertyName("mineable")]
    public bool Mineable { get; set; }

    [JsonPropertyName("buildable")]
    public bool Buildable { get; set; }

    [JsonPropertyName("smoothable")]
    public bool Smoothable { get; set; }

    [JsonPropertyName("nav_cost_base")]
    public int NavCostBase { get; set; } = 10;

    [JsonPropertyName("opacity")]
    public int Opacity { get; set; }

    [JsonPropertyName("flammable")]
    public bool Flammable { get; set; }

    [JsonPropertyName("layer_depth")]
    public int? LayerDepth { get; set; }

    [JsonPropertyName("ore_chance")]
    public float? OreChance { get; set; }
}
