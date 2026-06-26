using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Registry;

/// <summary>
/// Registry for terrain kind definitions loaded from content JSON.
/// </summary>
internal sealed class TerrainKindRegistry : IRuntimeTerrainKindCatalog
{
    private readonly Dictionary<byte, TerrainKindDefinition> _kindsById = new();
    private readonly Dictionary<string, byte> _kindsByName = new();
    private readonly List<TerrainKindDefinition> _allKinds = new();

    internal ContentVersion Version { get; private set; }
    internal TerrainBitLayout BitLayout { get; private set; } = new();

    /// <summary>
    /// Load terrain kinds from definitions
    /// </summary>
    internal void LoadTerrainKinds(IEnumerable<TerrainKindDefinition> kinds,
        TerrainBitLayout bitLayout, ContentVersion version)
    {
        Version = version;
        BitLayout = bitLayout;
        Clear();

        // Add all terrain kinds
        foreach (var kind in kinds)
        {
            AddTerrainKind(kind);
        }

        // Validate bit layout
        bitLayout.Validate();

        ContentRegistryDiagnostics.Emit($"[TerrainKindRegistry] Loaded {_kindsById.Count} terrain kinds, version {version}");
    }

    /// <summary>
    /// Add a terrain kind to the registry
    /// </summary>
    private void AddTerrainKind(TerrainKindDefinition kind)
    {
        // Validate
        kind.Validate();

        // Check for duplicate ID
        if (_kindsById.ContainsKey(kind.Id))
        {
            throw new InvalidOperationException(
                $"Duplicate terrain kind ID {kind.Id}: '{kind.Name}' and '{_kindsById[kind.Id].Name}'");
        }

        // Check for duplicate name
        if (_kindsByName.ContainsKey(kind.Name))
        {
            throw new InvalidOperationException(
                $"Duplicate terrain kind name '{kind.Name}'");
        }

        // Add to registries
        _kindsById[kind.Id] = kind;
        _kindsByName[kind.Name] = kind.Id;
        _allKinds.Add(kind);
    }

    /// <summary>
    /// Get terrain kind by ID
    /// </summary>
    internal TerrainKindDefinition? GetKind(byte id)
    {
        return _kindsById.GetValueOrDefault(id);
    }

    /// <summary>
    /// Get terrain kind by name
    /// </summary>
    internal TerrainKindDefinition? GetKind(string name)
    {
        if (_kindsByName.TryGetValue(name, out var id))
        {
            return _kindsById[id];
        }
        return null;
    }

    /// <summary>
    /// Get terrain kind ID by name
    /// </summary>
    internal byte? GetKindId(string name)
    {
        return _kindsByName.GetValueOrDefault(name);
    }

    /// <summary>
    /// Resolve terrain kind name to ID with fallback
    /// </summary>
    internal byte ResolveKind(string name, byte fallback = 0)
    {
        if (_kindsByName.TryGetValue(name, out var id))
        {
            return id;
        }

        ContentRegistryDiagnostics.Emit($"[TerrainKindRegistry] Warning: Unknown terrain kind '{name}', using fallback {fallback}");
        return fallback;
    }

    /// <summary>
    /// Check if a material can have a specific terrain kind
    /// </summary>
    internal bool IsMaterialAllowed(TerrainKindDefinition kind, string materialCategory)
    {
        // If no restrictions, allow all
        if (kind.AllowedMaterials.Count == 0)
            return true;

        // Check if category is in allowed list
        return kind.AllowedMaterials.Contains(materialCategory) ||
               kind.AllowedMaterials.Contains("*");
    }

    /// <summary>
    /// Get all terrain kinds that allow a material category
    /// </summary>
    internal IEnumerable<TerrainKindDefinition> GetKindsForMaterial(string materialCategory)
    {
        return _allKinds.Where(k => IsMaterialAllowed(k, materialCategory));
    }

    /// <summary>
    /// Build navigation mask from terrain kind
    /// </summary>
    internal byte BuildNavigationMask(TerrainKindDefinition kind)
    {
        byte mask = 0;

        if (kind.Navigation.Walkable) mask |= 1 << 0; // Walk
        if (kind.Navigation.Climbable) mask |= 1 << 1; // Crawl/Climb
        // Swim determined by fluid presence
        if (kind.Navigation.Flyable) mask |= 1 << 3; // Fly
        if (kind.Navigation.Standable) mask |= 1 << 4; // Standable
        if (kind.Navigation.Climbable) mask |= 1 << 5; // EdgeClimb

        return mask;
    }

    /// <summary>
    /// Clear the registry
    /// </summary>
    private void Clear()
    {
        _kindsById.Clear();
        _kindsByName.Clear();
        _allKinds.Clear();
    }

    internal IEnumerable<TerrainKindDefinition> GetAllKinds() => _allKinds;

    ContentVersion IRuntimeTerrainKindCatalog.Version => Version;

    TerrainBitLayout IRuntimeTerrainKindCatalog.BitLayout => BitLayout;

    TerrainKindDefinition? IRuntimeTerrainKindCatalog.GetKind(byte id) => GetKind(id);

    TerrainKindDefinition? IRuntimeTerrainKindCatalog.GetKind(string name) => GetKind(name);

    byte? IRuntimeTerrainKindCatalog.GetKindId(string name) => GetKindId(name);

    byte IRuntimeTerrainKindCatalog.ResolveKind(string name, byte fallback) => ResolveKind(name, fallback);

    bool IRuntimeTerrainKindCatalog.IsMaterialAllowed(TerrainKindDefinition kind, string materialCategory) =>
        IsMaterialAllowed(kind, materialCategory);

    IEnumerable<TerrainKindDefinition> IRuntimeTerrainKindCatalog.GetKindsForMaterial(string materialCategory) =>
        GetKindsForMaterial(materialCategory);

    byte IRuntimeTerrainKindCatalog.BuildNavigationMask(TerrainKindDefinition kind) => BuildNavigationMask(kind);

    IEnumerable<TerrainKindDefinition> IRuntimeTerrainKindCatalog.GetAllKinds() => GetAllKinds();
}
