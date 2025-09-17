using System;
using System.Collections.Generic;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Complete definition of a material loaded from JSON
/// </summary>
public class MaterialDefinition
{
    public ushort Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> Aliases { get; set; } = new();
    public string Category { get; set; } = "";

    // Display properties
    public DisplayProperties Display { get; set; } = new();

    // Physical properties
    public PhysicalProperties Physical { get; set; } = new();

    // Navigation properties
    public NavigationProperties Navigation { get; set; } = new();

    // Mining properties
    public MiningProperties Mining { get; set; } = new();

    // Temperature properties
    public TemperatureProperties Temperature { get; set; } = new();

    // Value properties
    public ValueProperties Value { get; set; } = new();

    // Tags
    public HashSet<string> Tags { get; set; } = new();

    // Validation
    public void Validate()
    {
        if (Id == 0)
            throw new InvalidOperationException($"Material '{Name}' has invalid ID 0 (reserved for 'none')");

        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Material must have a name");

        if (!System.Text.RegularExpressions.Regex.IsMatch(Name, "^[a-z][a-z0-9_]*$"))
            throw new InvalidOperationException($"Material name '{Name}' must be lowercase alphanumeric with underscores");

        if (string.IsNullOrWhiteSpace(Category))
            throw new InvalidOperationException($"Material '{Name}' must have a category");
    }
}

public class DisplayProperties
{
    public char Glyph { get; set; } = '?';
    public ConsoleColor Foreground { get; set; } = ConsoleColor.Gray;
    public ConsoleColor? Background { get; set; }
    public (byte R, byte G, byte B)? ParticleColor { get; set; }
}

public class PhysicalProperties
{
    public float Density { get; set; } = 1000f; // kg/m³
    public int Hardness { get; set; } = 5; // Mohs scale 0-10
    public int Toughness { get; set; } = 50; // 0-100
    public float Elasticity { get; set; } = 0.5f; // 0-1
    public float Friction { get; set; } = 0.5f; // 0-2
}

public class NavigationProperties
{
    // Materials only provide modifiers, not legality
    public byte MoveCostModifier { get; set; } = 100; // Base cost multiplier (percentage)
    public float Friction { get; set; } = 1.0f; // Surface friction (0=ice, 1=normal, 2=sticky)
    public int HazardLevel { get; set; } = 0; // 0-10
    public string HazardType { get; set; } = "none"; // heat, cold, poison, radiation, etc.
    public bool Slippery { get; set; } = false; // Causes slipping
    public bool Sticky { get; set; } = false; // Slows movement
}

public class MiningProperties
{
    public bool Mineable { get; set; } = true;
    public int DiggingTime { get; set; } = 100; // ticks
    public string ToolRequired { get; set; } = "pick";
    public int MinToolLevel { get; set; } = 0; // 0-10
    public List<MiningYield> Yields { get; set; } = new();
}

public class MiningYield
{
    public string Material { get; set; } = "";
    public int Min { get; set; } = 1;
    public int Max { get; set; } = 1;
    public float Probability { get; set; } = 1.0f;
}

public class TemperatureProperties
{
    public float? MeltingPoint { get; set; } // Kelvin
    public float? BoilingPoint { get; set; } // Kelvin
    public float? IgnitionPoint { get; set; } // Kelvin
    public float HeatCapacity { get; set; } = 1000f; // J/kg·K
    public float ThermalConductivity { get; set; } = 1.0f; // W/m·K
}

public class ValueProperties
{
    public float BaseValue { get; set; } = 1.0f;
    public string Rarity { get; set; } = "common";
    public bool Tradeable { get; set; } = true;
}