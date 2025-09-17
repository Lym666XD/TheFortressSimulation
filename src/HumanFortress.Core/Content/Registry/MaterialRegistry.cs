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
    private readonly Dictionary<string, ushort> _materialsByName = new();
    private readonly Dictionary<string, ushort> _aliases = new();
    private readonly List<MaterialDefinition> _allMaterials = new();

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

        // Add all materials
        foreach (var material in materials)
        {
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

        // Check for duplicate ID
        if (_materialsById.ContainsKey(material.Id))
        {
            throw new InvalidOperationException(
                $"Duplicate material ID {material.Id}: '{material.Name}' and '{_materialsById[material.Id].Name}'");
        }

        // Check for duplicate name
        if (_materialsByName.ContainsKey(material.Name))
        {
            throw new InvalidOperationException(
                $"Duplicate material name '{material.Name}'");
        }

        // Add to registries
        _materialsById[material.Id] = material;
        _materialsByName[material.Name] = material.Id;
        _allMaterials.Add(material);

        // Add aliases
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
            Name = "none",
            Category = "special",
            Display = new DisplayProperties { Glyph = ' ', Foreground = ConsoleColor.Black },
            Physical = new PhysicalProperties { Density = 0 },
            Navigation = new NavigationProperties
            {
                MoveCostModifier = 100,
                Friction = 1.0f
            }
        };
        _materialsById[None] = none;
        _materialsByName["none"] = None;

        // Void (MAX) - out of bounds
        var voidMat = new MaterialDefinition
        {
            Id = Void,
            Name = "void",
            Category = "special",
            Display = new DisplayProperties { Glyph = ' ', Foreground = ConsoleColor.Black },
            Physical = new PhysicalProperties { Density = 0 },
            Navigation = new NavigationProperties
            {
                MoveCostModifier = 255,
                Friction = 1.0f
            }
        };
        _materialsById[Void] = voidMat;
        _materialsByName["void"] = Void;

        // Air (MAX-1) - empty space
        var air = new MaterialDefinition
        {
            Id = Air,
            Name = "air",
            Category = "special",
            Display = new DisplayProperties { Glyph = ' ', Foreground = ConsoleColor.Black },
            Physical = new PhysicalProperties { Density = 1.2f },
            Navigation = new NavigationProperties
            {
                MoveCostModifier = 100,
                Friction = 1.0f
            }
        };
        _materialsById[Air] = air;
        _materialsByName["air"] = Air;

        // Bedrock (MAX-2) - unbreakable boundary
        var bedrock = new MaterialDefinition
        {
            Id = Bedrock,
            Name = "bedrock",
            Category = "special",
            Display = new DisplayProperties { Glyph = '#', Foreground = ConsoleColor.DarkGray },
            Physical = new PhysicalProperties { Density = 10000, Hardness = 10, Toughness = 100 },
            Navigation = new NavigationProperties
            {
                MoveCostModifier = 255,
                Friction = 1.0f
            },
            Mining = new MiningProperties { Mineable = false }
        };
        _materialsById[Bedrock] = bedrock;
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
    /// Clear the registry
    /// </summary>
    private void Clear()
    {
        _materialsById.Clear();
        _materialsByName.Clear();
        _aliases.Clear();
        _allMaterials.Clear();
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