using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Registry for geology definitions (tile prototypes)
/// </summary>
public class GeologyRegistry
{
    private readonly Dictionary<string, GeologyDefinition> _prototypesById = new();
    private readonly Dictionary<(string terrainKind, string material), GeologyDefinition> _prototypesByCombo = new();
    private readonly List<GeologyDefinition> _allPrototypes = new();

    public ContentVersion Version { get; private set; }

    /// <summary>
    /// Load geology prototypes from definitions
    /// </summary>
    public void LoadPrototypes(IEnumerable<GeologyDefinition> prototypes, ContentVersion version)
    {
        Version = version;
        Clear();

        foreach (var prototype in prototypes)
        {
            AddPrototype(prototype);
        }

        Console.WriteLine($"[GeologyRegistry] Loaded {_prototypesById.Count} geology prototypes, version {version}");
    }

    /// <summary>
    /// Add a geology prototype to the registry
    /// </summary>
    private void AddPrototype(GeologyDefinition prototype)
    {
        // Validate
        prototype.Validate();

        // Check for duplicate ID
        if (_prototypesById.ContainsKey(prototype.Id))
        {
            throw new InvalidOperationException($"Duplicate geology prototype ID: '{prototype.Id}'");
        }

        // Add to registries
        _prototypesById[prototype.Id] = prototype;
        _prototypesByCombo[(prototype.TerrainKind, prototype.Material)] = prototype;
        _allPrototypes.Add(prototype);
    }

    /// <summary>
    /// Get prototype by ID
    /// </summary>
    public GeologyDefinition? GetPrototype(string id)
    {
        return _prototypesById.GetValueOrDefault(id);
    }

    /// <summary>
    /// Get prototype by terrain kind and material combination
    /// </summary>
    public GeologyDefinition? GetPrototype(string terrainKind, string material)
    {
        return _prototypesByCombo.GetValueOrDefault((terrainKind, material));
    }

    /// <summary>
    /// Find or create a prototype for a terrain/material combination
    /// </summary>
    public GeologyDefinition GetOrCreatePrototype(string terrainKind, string material,
        TerrainKindDefinition? terrainDef = null, MaterialDefinition? materialDef = null)
    {
        // Check if we have an explicit prototype
        var existing = GetPrototype(terrainKind, material);
        if (existing != null)
            return existing;

        // Create a dynamic prototype
        var prototype = new GeologyDefinition
        {
            Id = $"dynamic_{terrainKind}_{material}",
            Name = $"{material} {terrainKind}",
            TerrainKind = terrainKind,
            Material = material
        };

        // Validate if definitions provided
        if (terrainDef != null && materialDef != null)
        {
            if (!prototype.IsValidCombination(terrainDef, materialDef))
            {
                Console.WriteLine($"[GeologyRegistry] Warning: Invalid combination {terrainKind} + {material}");
            }
        }

        return prototype;
    }

    /// <summary>
    /// Get all prototypes for a specific terrain kind
    /// </summary>
    public IEnumerable<GeologyDefinition> GetPrototypesForTerrainKind(string terrainKind)
    {
        return _allPrototypes.Where(p => p.TerrainKind == terrainKind);
    }

    /// <summary>
    /// Get all prototypes for a specific material
    /// </summary>
    public IEnumerable<GeologyDefinition> GetPrototypesForMaterial(string material)
    {
        return _allPrototypes.Where(p => p.Material == material);
    }

    /// <summary>
    /// Clear the registry
    /// </summary>
    private void Clear()
    {
        _prototypesById.Clear();
        _prototypesByCombo.Clear();
        _allPrototypes.Clear();
    }

    /// <summary>
    /// Get all prototypes
    /// </summary>
    public IEnumerable<GeologyDefinition> GetAllPrototypes() => _allPrototypes;
}