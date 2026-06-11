using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Registry for terrain kind definitions loaded from JSON
/// </summary>
public class TerrainKindRegistry
{
    private readonly Dictionary<byte, TerrainKindDefinition> _kindsById = new();
    private readonly Dictionary<string, byte> _kindsByName = new();
    private readonly List<TerrainKindDefinition> _allKinds = new();

    public ContentVersion Version { get; private set; }
    public TerrainBitLayout BitLayout { get; private set; } = new();

    /// <summary>
    /// Load terrain kinds from definitions
    /// </summary>
    public void LoadTerrainKinds(IEnumerable<TerrainKindDefinition> kinds,
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
    public TerrainKindDefinition? GetKind(byte id)
    {
        return _kindsById.GetValueOrDefault(id);
    }

    /// <summary>
    /// Get terrain kind by name
    /// </summary>
    public TerrainKindDefinition? GetKind(string name)
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
    public byte? GetKindId(string name)
    {
        return _kindsByName.GetValueOrDefault(name);
    }

    /// <summary>
    /// Resolve terrain kind name to ID with fallback
    /// </summary>
    public byte ResolveKind(string name, byte fallback = 0)
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
    public bool IsMaterialAllowed(TerrainKindDefinition kind, string materialCategory)
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
    public IEnumerable<TerrainKindDefinition> GetKindsForMaterial(string materialCategory)
    {
        return _allKinds.Where(k => IsMaterialAllowed(k, materialCategory));
    }

    /// <summary>
    /// Build navigation mask from terrain kind
    /// </summary>
    public byte BuildNavigationMask(TerrainKindDefinition kind)
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

    public IEnumerable<TerrainKindDefinition> GetAllKinds() => _allKinds;
}

/// <summary>
/// Describes the bit layout of the TerrainBits field
/// </summary>
public class TerrainBitLayout
{
    public int TotalBits { get; set; } = 16;
    public List<BitFieldDefinition> Fields { get; set; } = new();

    public void Validate()
    {
        if (TotalBits != 16)
            throw new InvalidOperationException("TerrainBits must be 16 bits");

        // Check for overlapping bit ranges
        for (int i = 0; i < Fields.Count; i++)
        {
            var field1 = Fields[i];
            for (int j = i + 1; j < Fields.Count; j++)
            {
                var field2 = Fields[j];
                if (BitRangesOverlap(field1, field2))
                {
                    throw new InvalidOperationException(
                        $"Bit fields '{field1.Name}' and '{field2.Name}' overlap");
                }
            }
        }

        // Check that all fields fit within TotalBits
        foreach (var field in Fields)
        {
            if (field.StartBit + field.BitCount > TotalBits)
            {
                throw new InvalidOperationException(
                    $"Bit field '{field.Name}' exceeds total bit count");
            }
        }
    }

    private bool BitRangesOverlap(BitFieldDefinition a, BitFieldDefinition b)
    {
        int aEnd = a.StartBit + a.BitCount - 1;
        int bEnd = b.StartBit + b.BitCount - 1;
        return !(aEnd < b.StartBit || bEnd < a.StartBit);
    }
}

public class BitFieldDefinition
{
    public string Name { get; set; } = "";
    public int StartBit { get; set; }
    public int BitCount { get; set; }
    public string Description { get; set; } = "";
    public Dictionary<string, int> Values { get; set; } = new();
}
