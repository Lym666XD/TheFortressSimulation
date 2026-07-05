using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Registry;

internal sealed partial class ContentRegistry
{
    /// <summary>
    /// Load materials from JSON
    /// </summary>
    private async Task<List<MaterialDefinition>> LoadMaterialsAsync(string file, Dictionary<string, JsonDocument> schemas, bool isAuthoringFormat = true)
    {
        if (!File.Exists(file))
        {
            ValidationResult.Errors.Add($"Materials file not found: {file}");
            return new List<MaterialDefinition>();
        }

        var json = await File.ReadAllTextAsync(file);
        var doc = JsonDocument.Parse(json);

        // Get version
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("version", out var versionElem))
        {
            Version = ContentVersion.Parse(versionElem.GetString() ?? "1.0.0");
        }

        // Parse materials
        var materials = new List<MaterialDefinition>();

        // Handle both array format and object format with "materials" property
        JsonElement materialsElem;
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            materialsElem = doc.RootElement;
        }
        else if (doc.RootElement.TryGetProperty("materials", out var matProp))
        {
            materialsElem = matProp;
        }
        else
        {
            ValidationResult.Errors.Add($"Invalid materials file format: {file}");
            return materials;
        }

        foreach (var elem in materialsElem.EnumerateArray())
        {
            try
            {
                var material = MaterialParser.ParseMaterial(elem, isAuthoringFormat);
                materials.Add(material);
            }
            catch (Exception ex)
            {
                ValidationResult.Errors.Add($"Error parsing material: {ex.Message}");
            }
        }

        // Old code for backwards compatibility - only use if MaterialParser fails
        if (materials.Count == 0
            && doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("materials", out var oldMaterialsElem))
        {
            foreach (var elem in oldMaterialsElem.EnumerateArray())
            {
                try
                {
                    var material = ParseMaterialDefinition(elem);
                    materials.Add(material);
                }
                catch (Exception ex)
                {
                    ValidationResult.Errors.Add($"Error parsing material: {ex.Message}");
                }
            }
        }

        // Load into registry
        _materials.LoadMaterials(materials, Version);

        return materials;
    }

    /// <summary>
    /// Parse a material definition from JSON using the new v4 parser
    /// </summary>
    private MaterialDefinition ParseMaterialDefinition(JsonElement elem)
    {
        // Use the new MaterialParser which supports both v3 (legacy) and v4 formats
        // Assume authoring format (human-readable values) - parser will auto-convert to FX
        return MaterialParser.ParseMaterial(elem, isAuthoringFormat: true);
    }

    /// <summary>
    /// Load terrain kinds from JSON
    /// </summary>
    private async Task<List<TerrainKindDefinition>> LoadTerrainKindsAsync(string file, Dictionary<string, JsonDocument> schemas)
    {
        if (!File.Exists(file))
        {
            ValidationResult.Errors.Add($"Terrain kinds file not found: {file}");
            return new List<TerrainKindDefinition>();
        }

        var json = await File.ReadAllTextAsync(file);
        var doc = JsonDocument.Parse(json);

        var kinds = new List<TerrainKindDefinition>();
        TerrainBitLayout? bitLayout = null;

        // Parse terrain kinds
        if (doc.RootElement.TryGetProperty("terrainKinds", out var kindsElem))
        {
            foreach (var elem in kindsElem.EnumerateArray())
            {
                try
                {
                    var kind = ParseTerrainKindDefinition(elem);
                    kinds.Add(kind);
                }
                catch (Exception ex)
                {
                    ValidationResult.Errors.Add($"Error parsing terrain kind: {ex.Message}");
                }
            }
        }

        // Parse bit layout
        if (doc.RootElement.TryGetProperty("terrainBitLayout", out var layoutElem))
        {
            bitLayout = ParseTerrainBitLayout(layoutElem);
        }

        // Load into registry
        _terrainKinds.LoadTerrainKinds(kinds, bitLayout ?? new TerrainBitLayout(), Version);

        return kinds;
    }

    private TerrainKindDefinition ParseTerrainKindDefinition(JsonElement elem)
    {
        var kind = new TerrainKindDefinition
        {
            Id = elem.GetProperty("id").GetByte(),
            Name = elem.GetProperty("name").GetString() ?? "",
            Shape = elem.GetProperty("shape").GetString() ?? "solid"
        };

        if (elem.TryGetProperty("description", out var desc))
            kind.Description = desc.GetString() ?? "";

        // Parse navigation rules
        if (elem.TryGetProperty("navigation", out var nav))
        {
            kind.Navigation = ParseTerrainNavigationRules(nav);
        }

        // Parse support rules
        if (elem.TryGetProperty("support", out var support))
        {
            kind.Support = ParseTerrainSupportRules(support);
        }

        // Parse connectivity rules
        if (elem.TryGetProperty("connectivity", out var connectivity))
        {
            kind.Connectivity = ParseTerrainConnectivityRules(connectivity);
        }

        // Parse allowed materials
        if (elem.TryGetProperty("allowedMaterials", out var allowedMaterials))
        {
            foreach (var mat in allowedMaterials.EnumerateArray())
            {
                kind.AllowedMaterials.Add(mat.GetString() ?? "");
            }
        }

        // Parse flags
        if (elem.TryGetProperty("flags", out var flags))
        {
            kind.Flags = ParseTerrainFlags(flags);
        }

        return kind;
    }

    private TerrainNavigationRules ParseTerrainNavigationRules(JsonElement elem)
    {
        var rules = new TerrainNavigationRules();

        if (elem.TryGetProperty("walkable", out var walkable))
            rules.Walkable = walkable.GetBoolean();

        if (elem.TryGetProperty("standable", out var standable))
            rules.Standable = standable.GetBoolean();

        if (elem.TryGetProperty("climbable", out var climbable))
            rules.Climbable = climbable.GetBoolean();

        if (elem.TryGetProperty("flyable", out var flyable))
            rules.Flyable = flyable.GetBoolean();

        if (elem.TryGetProperty("blocksSight", out var blocksSight))
            rules.BlocksSight = blocksSight.GetBoolean();

        if (elem.TryGetProperty("blocksProjectiles", out var blocksProjectiles))
            rules.BlocksProjectiles = blocksProjectiles.GetBoolean();

        if (elem.TryGetProperty("blocksGas", out var blocksGas))
            rules.BlocksGas = blocksGas.GetBoolean();

        if (elem.TryGetProperty("blocksLiquid", out var blocksLiquid))
            rules.BlocksLiquid = blocksLiquid.GetBoolean();

        if (elem.TryGetProperty("baseCost", out var baseCost))
            rules.BaseCost = baseCost.GetByte();

        if (elem.TryGetProperty("costMultiplier", out var costMultiplier))
            rules.CostMultiplier = costMultiplier.GetSingle();

        if (elem.TryGetProperty("allowsZUp", out var allowsZUp))
            rules.AllowsZUp = allowsZUp.GetBoolean();

        if (elem.TryGetProperty("allowsZDown", out var allowsZDown))
            rules.AllowsZDown = allowsZDown.GetBoolean();

        if (elem.TryGetProperty("requiresDirection", out var requiresDirection))
            rules.RequiresDirection = requiresDirection.GetBoolean();

        return rules;
    }

    private TerrainSupportRules ParseTerrainSupportRules(JsonElement elem)
    {
        var rules = new TerrainSupportRules();

        if (elem.TryGetProperty("providesSupport", out var providesSupport))
            rules.ProvidesSupport = providesSupport.GetBoolean();

        if (elem.TryGetProperty("requiresSupport", out var requiresSupport))
            rules.RequiresSupport = requiresSupport.GetBoolean();

        if (elem.TryGetProperty("lateralSupport", out var lateralSupport))
            rules.LateralSupport = lateralSupport.GetBoolean();

        if (elem.TryGetProperty("maxLoadAbove", out var maxLoadAbove))
            rules.MaxLoadAbove = maxLoadAbove.GetInt32();

        if (elem.TryGetProperty("collapseInto", out var collapseInto))
            rules.CollapseInto = collapseInto.GetString();

        return rules;
    }

    private TerrainConnectivityRules ParseTerrainConnectivityRules(JsonElement elem)
    {
        var rules = new TerrainConnectivityRules();

        if (elem.TryGetProperty("connectsHorizontal", out var connectsHorizontal))
            rules.ConnectsHorizontal = connectsHorizontal.GetBoolean();

        if (elem.TryGetProperty("connectsVertical", out var connectsVertical))
            rules.ConnectsVertical = connectsVertical.GetBoolean();

        if (elem.TryGetProperty("connectsDiagonal", out var connectsDiagonal))
            rules.ConnectsDiagonal = connectsDiagonal.GetBoolean();

        if (elem.TryGetProperty("allowsZTransition", out var allowsZTransition))
            rules.AllowsZTransition = allowsZTransition.GetBoolean();

        if (elem.TryGetProperty("transitionType", out var transitionType))
            rules.TransitionType = transitionType.GetString() ?? "none";

        return rules;
    }

    private TerrainFlags ParseTerrainFlags(JsonElement elem)
    {
        var flags = new TerrainFlags();

        if (elem.TryGetProperty("natural", out var natural))
            flags.Natural = natural.GetBoolean();

        if (elem.TryGetProperty("smoothable", out var smoothable))
            flags.Smoothable = smoothable.GetBoolean();

        if (elem.TryGetProperty("engravable", out var engravable))
            flags.Engravable = engravable.GetBoolean();

        if (elem.TryGetProperty("buildable", out var buildable))
            flags.Buildable = buildable.GetBoolean();

        if (elem.TryGetProperty("removable", out var removable))
            flags.Removable = removable.GetBoolean();

        if (elem.TryGetProperty("replaceable", out var replaceable))
            flags.Replaceable = replaceable.GetBoolean();

        return flags;
    }

    private TerrainBitLayout ParseTerrainBitLayout(JsonElement elem)
    {
        var layout = new TerrainBitLayout();

        if (elem.TryGetProperty("totalBits", out var totalBits))
            layout.TotalBits = totalBits.GetInt32();

        if (elem.TryGetProperty("fields", out var fields))
        {
            foreach (var field in fields.EnumerateArray())
            {
                var bitField = new BitFieldDefinition
                {
                    Name = field.GetProperty("name").GetString() ?? "",
                    StartBit = field.GetProperty("startBit").GetInt32(),
                    BitCount = field.GetProperty("bitCount").GetInt32()
                };

                if (field.TryGetProperty("description", out var desc))
                    bitField.Description = desc.GetString() ?? "";

                if (field.TryGetProperty("values", out var values))
                {
                    foreach (var prop in values.EnumerateObject())
                    {
                        bitField.Values[prop.Name] = prop.Value.GetInt32();
                    }
                }

                layout.Fields.Add(bitField);
            }
        }

        return layout;
    }
}
