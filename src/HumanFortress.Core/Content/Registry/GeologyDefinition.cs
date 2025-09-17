using System;
using System.Collections.Generic;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Defines a tile prototype (combination of TerrainKind + Material)
/// This is the actual "recipe" for creating tiles in the world
/// </summary>
public class GeologyDefinition
{
    /// <summary>
    /// Unique identifier for this geology prototype
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The terrain kind (shape) - references terrain_kinds.json
    /// </summary>
    public string TerrainKind { get; set; } = "";

    /// <summary>
    /// The material - references materials.registry.json
    /// </summary>
    public string Material { get; set; } = "";

    /// <summary>
    /// Per-prototype navigation cost override
    /// If null, uses TerrainKind base + Material modifier
    /// </summary>
    public byte? NavCostBase { get; set; }

    /// <summary>
    /// Per-prototype opacity override (for line of sight)
    /// If null, uses TerrainKind default
    /// </summary>
    public byte? Opacity { get; set; }

    /// <summary>
    /// Tags for this specific combination
    /// </summary>
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// Validate the geology definition
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new InvalidOperationException("Geology prototype must have an ID");

        if (string.IsNullOrWhiteSpace(TerrainKind))
            throw new InvalidOperationException($"Geology prototype '{Id}' must specify a terrain kind");

        if (string.IsNullOrWhiteSpace(Material))
            throw new InvalidOperationException($"Geology prototype '{Id}' must specify a material");
    }

    /// <summary>
    /// Calculate the final movement cost for this tile prototype
    /// </summary>
    public byte CalculateMoveCost(TerrainKindDefinition terrainKind, MaterialDefinition material)
    {
        // If prototype has override, use it
        if (NavCostBase.HasValue)
            return NavCostBase.Value;

        // Otherwise calculate from terrain kind base cost and material modifier
        byte baseCost = 100; // Default base cost

        // Apply terrain kind multiplier
        baseCost = (byte)(baseCost * terrainKind.Navigation.CostMultiplier);

        // Apply material modifier (as percentage)
        baseCost = (byte)(baseCost * material.Navigation.MoveCostModifier / 100);

        // Clamp to valid range
        return (byte)Math.Clamp(baseCost, (byte)1, (byte)255);
    }

    /// <summary>
    /// Check if this geology is valid for a given material/terrain combination
    /// </summary>
    public bool IsValidCombination(TerrainKindDefinition terrainKind, MaterialDefinition material)
    {
        // Check if material category is allowed for this terrain kind
        if (terrainKind.AllowedMaterials.Count > 0)
        {
            if (!terrainKind.AllowedMaterials.Contains(material.Category) &&
                !terrainKind.AllowedMaterials.Contains("*") &&
                !terrainKind.AllowedMaterials.Contains(material.Name))
            {
                return false;
            }
        }

        // Additional validation rules can go here
        // For example, liquids can't be walls, gases can't be floors, etc.

        return true;
    }
}