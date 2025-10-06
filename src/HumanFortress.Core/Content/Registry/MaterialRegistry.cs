using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Registry for all material definitions loaded from JSON
/// </summary>
public class MaterialRegistry
{
    private readonly Dictionary<ushort, MaterialDefinition> _materialsById = new();
    private readonly Dictionary<string, ushort> _materialsByStringId = new();  // string ID → numeric ID
    private readonly Dictionary<string, ushort> _materialsByName = new();  // legacy compatibility
    private readonly Dictionary<string, ushort> _aliases = new();
    private readonly List<MaterialDefinition> _allMaterials = new();

    private ushort _nextNumericId = 10;  // Start after reserved IDs (0-9)

    public ContentVersion Version { get; private set; }
    public string ContentHash { get; private set; } = "";

    /// <summary>
    /// Reserved material IDs
    /// </summary>
    public const ushort None = 0;
    public const ushort Void = ushort.MaxValue;
    public const ushort Air = ushort.MaxValue - 1;
    public const ushort Bedrock = ushort.MaxValue - 2;

    /// <summary>
    /// Load materials from definitions
    /// </summary>
    public void LoadMaterials(IEnumerable<MaterialDefinition> materials, ContentVersion version)
    {
        Version = version;
        Clear();

        // Add reserved materials
        AddReservedMaterials();

        // Sort materials by StringId for stable ID assignment across loads
        var sortedMaterials = materials.OrderBy(m => m.StringId).ToList();

        // Assign numeric IDs and add materials
        foreach (var material in sortedMaterials)
        {
            // Assign next available numeric ID
            material.Id = _nextNumericId++;
            AddMaterial(material);
        }

        // Compute content hash for save compatibility
        ComputeContentHash();

        Console.WriteLine($"[MaterialRegistry] Loaded {_materialsById.Count} materials, version {version}");
    }

    /// <summary>
    /// Add a material to the registry
    /// </summary>
    private void AddMaterial(MaterialDefinition material)
    {
        // Validate
        material.Validate();

        // Check for duplicate numeric ID
        if (_materialsById.ContainsKey(material.Id))
        {
            throw new InvalidOperationException(
                $"Duplicate material numeric ID {material.Id}: '{material.StringId}' and '{_materialsById[material.Id].StringId}'");
        }

        // Check for duplicate string ID
        if (_materialsByStringId.ContainsKey(material.StringId))
        {
            throw new InvalidOperationException(
                $"Duplicate material string ID '{material.StringId}'");
        }

        // Add to registries
        _materialsById[material.Id] = material;
        _materialsByStringId[material.StringId] = material.Id;
        _materialsByName[material.Name] = material.Id;  // legacy
        _allMaterials.Add(material);

        // Add aliases (both to string ID and name)
        foreach (var alias in material.Aliases)
        {
            if (_aliases.ContainsKey(alias))
            {
                Console.WriteLine($"[MaterialRegistry] Warning: Alias '{alias}' already exists, skipping");
                continue;
            }
            _aliases[alias] = material.Id;
        }
    }

    /// <summary>
    /// Add reserved materials that always exist
    /// </summary>
    private void AddReservedMaterials()
    {
        // None (0) - represents absence of material
        var none = new MaterialDefinition
        {
            Id = None,
            StringId = "none",
            Name = "none",
            Category = "special",
            Tags = new HashSet<string> { "special", "empty" },
            DensitySolid = 0,
            Display = new DisplayProperties { Glyph = ' ', Foreground = ConsoleColor.Black },
            Navigation = new MaterialNavigation
            {
                MoveCostAdd = 0,
                FrictionMulFx = FixedPoint.FX
            }
        };
        _materialsById[None] = none;
        _materialsByStringId["none"] = None;
        _materialsByName["none"] = None;

        // Void (MAX) - out of bounds
        var voidMat = new MaterialDefinition
        {
            Id = Void,
            StringId = "void",
            Name = "void",
            Category = "special",
            Tags = new HashSet<string> { "special", "impassable" },
            DensitySolid = 0,
            Display = new DisplayProperties { Glyph = ' ', Foreground = ConsoleColor.Black },
            Navigation = new MaterialNavigation
            {
                MoveCostAdd = 50,
                FrictionMulFx = FixedPoint.FX
            }
        };
        _materialsById[Void] = voidMat;
        _materialsByStringId["void"] = Void;
        _materialsByName["void"] = Void;

        // Air (MAX-1) - empty space
        var air = new MaterialDefinition
        {
            Id = Air,
            StringId = "air",
            Name = "air",
            Category = "gas",
            Tags = new HashSet<string> { "gas", "empty" },
            DensitySolid = 1,
            Display = new DisplayProperties { Glyph = ' ', Foreground = ConsoleColor.Black },
            Navigation = new MaterialNavigation
            {
                MoveCostAdd = 0,
                FrictionMulFx = FixedPoint.FX
            }
        };
        _materialsById[Air] = air;
        _materialsByStringId["air"] = Air;
        _materialsByName["air"] = Air;

        // Bedrock (MAX-2) - unbreakable boundary
        var bedrock = new MaterialDefinition
        {
            Id = Bedrock,
            StringId = "bedrock",
            Name = "bedrock",
            Category = "stone",
            Tags = new HashSet<string> { "stone", "unbreakable", "impassable" },
            DensitySolid = 10000,
            Display = new DisplayProperties { Glyph = '#', Foreground = ConsoleColor.DarkGray },
            Mechanics = new MaterialMechanics
            {
                HardnessEdgeFx = FixedPoint.FX * 10, // Extremely hard
                ToughnessFracFx = FixedPoint.FX * 10, // Extremely tough
                RigidityFx = FixedPoint.FX * 10 // Extremely rigid
            },
            Navigation = new MaterialNavigation
            {
                MoveCostAdd = 50,
                FrictionMulFx = FixedPoint.FX
            }
        };
        _materialsById[Bedrock] = bedrock;
        _materialsByStringId["bedrock"] = Bedrock;
        _materialsByName["bedrock"] = Bedrock;
    }

    /// <summary>
    /// Get material by ID
    /// </summary>
    public MaterialDefinition? GetMaterial(ushort id)
    {
        return _materialsById.GetValueOrDefault(id);
    }

    /// <summary>
    /// Get material by name (checks aliases too)
    /// </summary>
    public MaterialDefinition? GetMaterial(string name)
    {
        // Try direct name first
        if (_materialsByName.TryGetValue(name, out var id))
        {
            return _materialsById[id];
        }

        // Try aliases
        if (_aliases.TryGetValue(name, out id))
        {
            return _materialsById[id];
        }

        return null;
    }

    /// <summary>
    /// Get material ID by name
    /// </summary>
    public ushort? GetMaterialId(string name)
    {
        // Try direct name first
        if (_materialsByName.TryGetValue(name, out var id))
        {
            return id;
        }

        // Try aliases
        if (_aliases.TryGetValue(name, out id))
        {
            return id;
        }

        return null;
    }

    /// <summary>
    /// Resolve material name to ID with fallback
    /// </summary>
    public ushort ResolveMaterial(string name, ushort fallback = None)
    {
        var id = GetMaterialId(name);
        if (id.HasValue)
        {
            return id.Value;
        }

        Console.WriteLine($"[MaterialRegistry] Warning: Unknown material '{name}', using fallback {fallback}");
        return fallback;
    }

    /// <summary>
    /// Get all materials in a category
    /// </summary>
    public IEnumerable<MaterialDefinition> GetMaterialsByCategory(string category)
    {
        return _allMaterials.Where(m => m.Category == category);
    }

    /// <summary>
    /// Get all materials with a tag
    /// </summary>
    public IEnumerable<MaterialDefinition> GetMaterialsByTag(string tag)
    {
        return _allMaterials.Where(m => m.Tags.Contains(tag));
    }

    /// <summary>
    /// Check if material exists
    /// </summary>
    public bool HasMaterial(ushort id) => _materialsById.ContainsKey(id);
    public bool HasMaterial(string name) => _materialsByName.ContainsKey(name) || _aliases.ContainsKey(name);

    /// <summary>
    /// Get a snapshot of name to ID mappings for saves
    /// </summary>
    public Dictionary<string, ushort> GetNameToIdSnapshot()
    {
        return new Dictionary<string, ushort>(_materialsByName);
    }

    /// <summary>
    /// Apply aliases for save migration
    /// </summary>
    public void ApplyAliases(Dictionary<string, string> aliasMap)
    {
        foreach (var (oldName, newName) in aliasMap)
        {
            if (_materialsByName.TryGetValue(newName, out var id))
            {
                _aliases[oldName] = id;
                Console.WriteLine($"[MaterialRegistry] Applied alias: '{oldName}' -> '{newName}' (ID: {id})");
            }
            else
            {
                Console.WriteLine($"[MaterialRegistry] Warning: Cannot apply alias '{oldName}' -> '{newName}', target not found");
            }
        }
    }

    /// <summary>
    /// Resolve material string ID to numeric ID (supports aliases)
    /// </summary>
    public ushort? ResolveStringId(string stringId)
    {
        // Try direct string ID first
        if (_materialsByStringId.TryGetValue(stringId, out var id))
        {
            return id;
        }

        // Try aliases
        if (_aliases.TryGetValue(stringId, out id))
        {
            return id;
        }

        return null;
    }

    /// <summary>
    /// Clear the registry
    /// </summary>
    private void Clear()
    {
        _materialsById.Clear();
        _materialsByStringId.Clear();
        _materialsByName.Clear();
        _aliases.Clear();
        _allMaterials.Clear();
        _nextNumericId = 10;  // Reset ID counter
    }

    /// <summary>
    /// Compute content hash for save compatibility checking
    /// </summary>
    private void ComputeContentHash()
    {
        var sorted = _allMaterials
            .OrderBy(m => m.Id)
            .Select(m => $"{m.Id}:{m.Name}:{m.Category}")
            .ToList();

        var combined = string.Join("|", sorted);
        ContentHash = ComputeHash(combined);
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
    }
}