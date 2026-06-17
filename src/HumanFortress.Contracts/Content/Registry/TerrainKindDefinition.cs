using System;
using System.Collections.Generic;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Complete definition of a terrain kind (shape/form) loaded from JSON.
/// </summary>
public class TerrainKindDefinition
{
    public byte Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Shape { get; set; } = "solid";

    // Navigation rules
    public TerrainNavigationRules Navigation { get; set; } = new();

    // Support rules
    public TerrainSupportRules Support { get; set; } = new();

    // Connectivity rules
    public TerrainConnectivityRules Connectivity { get; set; } = new();

    // Allowed material categories
    public List<string> AllowedMaterials { get; set; } = new();

    // Terrain flags
    public TerrainFlags Flags { get; set; } = new();

    public void Validate()
    {
        if (Id > 7)
            throw new InvalidOperationException($"Terrain kind '{Name}' has ID {Id} > 7 (3-bit limit)");

        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Terrain kind must have a name");

        if (!System.Text.RegularExpressions.Regex.IsMatch(Name, "^[a-z][a-z0-9_]*$"))
            throw new InvalidOperationException($"Terrain kind name '{Name}' must be lowercase alphanumeric with underscores");

        var validShapes = new HashSet<string> { "solid", "floor", "empty", "ramp", "stairs", "chasm" };
        if (!validShapes.Contains(Shape))
            throw new InvalidOperationException($"Invalid shape '{Shape}' for terrain kind '{Name}'");
    }
}

public class TerrainNavigationRules
{
    // TerrainKind owns legality - these are hard rules
    public bool Walkable { get; set; }
    public bool Standable { get; set; }
    public bool Climbable { get; set; } // Reserved for future climbing system
    public bool Flyable { get; set; } = true;

    // Blocking rules
    public bool BlocksSight { get; set; }
    public bool BlocksProjectiles { get; set; }
    public bool BlocksGas { get; set; }
    public bool BlocksLiquid { get; set; }

    // Base cost for this terrain shape (before material modifiers)
    public byte BaseCost { get; set; } = 100;
    public float CostMultiplier { get; set; } = 1.0f; // Legacy, prefer BaseCost

    // Z-level transitions
    public bool AllowsZUp { get; set; } = false; // Stairs up, ramps
    public bool AllowsZDown { get; set; } = false; // Stairs down, ramps
    public bool RequiresDirection { get; set; } // For ramps/stairs
}

public class TerrainSupportRules
{
    public bool ProvidesSupport { get; set; } = true;
    public bool RequiresSupport { get; set; } = false;
    public bool LateralSupport { get; set; } = false;
    public int MaxLoadAbove { get; set; } = int.MaxValue;
    public string? CollapseInto { get; set; }
}

public class TerrainConnectivityRules
{
    public bool ConnectsHorizontal { get; set; } = true;
    public bool ConnectsVertical { get; set; } = false;
    public bool ConnectsDiagonal { get; set; } = false;
    public bool AllowsZTransition { get; set; } = false;
    public string TransitionType { get; set; } = "none"; // none, up, down, both
}

public class TerrainFlags
{
    public bool Natural { get; set; } = true;
    public bool Smoothable { get; set; } = false;
    public bool Engravable { get; set; } = false;
    public bool Buildable { get; set; } = false;
    public bool Removable { get; set; } = true;
    public bool Replaceable { get; set; } = true;
}
