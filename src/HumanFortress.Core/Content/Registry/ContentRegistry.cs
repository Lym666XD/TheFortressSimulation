using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HumanFortress.Core.Content.Registry;

/// <summary>
/// Main content registry that manages all loaded content
/// </summary>
public class ContentRegistry
{
    private static ContentRegistry? _instance;
    public static ContentRegistry Instance => _instance ??= new ContentRegistry();

    public MaterialRegistry Materials { get; } = new();
    public TerrainKindRegistry TerrainKinds { get; } = new();
    public GeologyRegistry Geology { get; } = new();
    public BiomeTemplateRegistry BiomeTemplates { get; } = new();
    public AliasResolver AliasResolver { get; } = new();

    public ContentVersion Version { get; private set; }
    public string ContentPath { get; private set; } = "";
    public string ContentHash { get; private set; } = "";
    public bool IsLoaded { get; private set; }

    // Validation statistics
    public ContentValidationResult ValidationResult { get; private set; } = new();

    private ContentRegistry() { }

    /// <summary>
    /// Load all content from the specified path
    /// </summary>
    public async Task LoadContentAsync(string contentPath)
    {
        Console.WriteLine($"[ContentRegistry] Loading content from: {contentPath}");
        ContentPath = contentPath;

        try
        {
            // Load schemas for validation
            var schemas = await LoadSchemasAsync(Path.Combine(contentPath, "schemas"));

            // Load and validate materials (prefer authoring format, fall back to registry format)
            var materialsAuthoring = Path.Combine(contentPath, "registries", "materials.authoring.json");
            var materialsRegistry = Path.Combine(contentPath, "registries", "materials.registry.json");
            var materialsLegacy = Path.Combine(contentPath, "registries", "materials.json");

            string materialsFile;
            bool isAuthoringFormat = true;

            if (File.Exists(materialsAuthoring))
            {
                materialsFile = materialsAuthoring;
                Console.WriteLine("[ContentRegistry] Loading materials from authoring format");
            }
            else if (File.Exists(materialsRegistry))
            {
                materialsFile = materialsRegistry;
                isAuthoringFormat = false;
                Console.WriteLine("[ContentRegistry] Loading materials from registry format");
            }
            else
            {
                materialsFile = materialsLegacy;
                Console.WriteLine("[ContentRegistry] Loading materials from legacy format");
            }

            var materials = await LoadMaterialsAsync(materialsFile, schemas, isAuthoringFormat);

            // Load and validate terrain kinds
            var terrainKindsFile = Path.Combine(contentPath, "registries", "terrain_kinds.json");
            var terrainKinds = await LoadTerrainKindsAsync(terrainKindsFile, schemas);

            // Load and validate geology prototypes
            var geologyFile = Path.Combine(contentPath, "registries", "geology.json");
            var geologyPrototypes = await LoadGeologyAsync(geologyFile, schemas);

            // Load and validate biome templates
            var biomesPath = Path.Combine(contentPath, "templates", "biomes");
            var biomeTemplates = await LoadBiomeTemplatesAsync(biomesPath, schemas);

            // Load aliases
            var aliasesFile = Path.Combine(contentPath, "registries", "aliases.json");
            if (File.Exists(aliasesFile))
            {
                await LoadAliasesAsync(aliasesFile, schemas);
            }

            // Cross-validate references
            CrossValidateContent();

            // Compute overall content hash
            ComputeContentHash();

            IsLoaded = true;
            Console.WriteLine($"[ContentRegistry] Content loaded successfully. Version: {Version}, Hash: {ContentHash}");
            Console.WriteLine($"[ContentRegistry] Validation: {ValidationResult.Warnings.Count} warnings, {ValidationResult.Errors.Count} errors");

            // Print validation errors
            if (ValidationResult.Errors.Count > 0)
            {
                Console.WriteLine("[ContentRegistry] Errors:");
                foreach (var error in ValidationResult.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }

            // Print validation warnings
            if (ValidationResult.Warnings.Count > 0)
            {
                Console.WriteLine("[ContentRegistry] Warnings:");
                foreach (var warning in ValidationResult.Warnings)
                {
                    Console.WriteLine($"  - {warning}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ContentRegistry] Error loading content: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Load content synchronously (for compatibility)
    /// </summary>
    public void LoadContent(string contentPath)
    {
        LoadContentAsync(contentPath).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Load schemas for validation
    /// </summary>
    private async Task<Dictionary<string, JsonDocument>> LoadSchemasAsync(string schemasPath)
    {
        var schemas = new Dictionary<string, JsonDocument>();

        if (!Directory.Exists(schemasPath))
        {
            Console.WriteLine($"[ContentRegistry] Warning: Schemas directory not found: {schemasPath}");
            return schemas;
        }

        foreach (var file in Directory.GetFiles(schemasPath, "*.schema.json"))
        {
            var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
            var json = await File.ReadAllTextAsync(file);
            schemas[name] = JsonDocument.Parse(json);
            Console.WriteLine($"[ContentRegistry] Loaded schema: {name}");
        }

        return schemas;
    }

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
        if (doc.RootElement.TryGetProperty("version", out var versionElem))
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
        if (materials.Count == 0 && doc.RootElement.TryGetProperty("materials", out var oldMaterialsElem))
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
        Materials.LoadMaterials(materials, Version);

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
        TerrainKinds.LoadTerrainKinds(kinds, bitLayout ?? new TerrainBitLayout(), Version);

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

    /// <summary>
    /// Load biome templates
    /// </summary>
    private async Task<List<BiomeTemplateDefinition>> LoadBiomeTemplatesAsync(string path, Dictionary<string, JsonDocument> schemas)
    {
        var templates = new List<BiomeTemplateDefinition>();

        if (!Directory.Exists(path))
        {
            ValidationResult.Warnings.Add($"Biome templates directory not found: {path}");
            return templates;
        }

        foreach (var file in Directory.GetFiles(path, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("templates", out var templatesElem))
                {
                    foreach (var elem in templatesElem.EnumerateArray())
                    {
                        try
                        {
                            var template = ParseBiomeTemplate(elem);
                            templates.Add(template);
                        }
                        catch (Exception ex)
                        {
                            ValidationResult.Errors.Add($"Error parsing biome template in {file}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ValidationResult.Errors.Add($"Error loading biome template file {file}: {ex.Message}");
            }
        }

        BiomeTemplates.LoadTemplates(templates, Version);
        return templates;
    }

    private BiomeTemplateDefinition ParseBiomeTemplate(JsonElement elem)
    {
        var template = new BiomeTemplateDefinition
        {
            Id = elem.GetProperty("id").GetString() ?? "",
            Name = elem.GetProperty("name").GetString() ?? ""
        };

        if (elem.TryGetProperty("description", out var desc))
            template.Description = desc.GetString() ?? "";

        if (elem.TryGetProperty("priority", out var priority))
            template.Priority = priority.GetInt32();

        // Parse layers
        if (elem.TryGetProperty("layers", out var layers))
        {
            foreach (var layer in layers.EnumerateArray())
            {
                template.Layers.Add(ParseLayerDefinition(layer));
            }
        }

        // Additional template parsing would go here...

        return template;
    }

    private LayerDefinition ParseLayerDefinition(JsonElement elem)
    {
        var layer = new LayerDefinition();

        // Parse material (string or distribution)
        if (elem.TryGetProperty("material", out var material))
        {
            if (material.ValueKind == JsonValueKind.String)
            {
                layer.Material = material.GetString() ?? "";
            }
            else
            {
                layer.MaterialDistribution = ParseMaterialDistribution(material);
            }
        }

        // Parse depth
        if (elem.TryGetProperty("depth", out var depth))
        {
            if (depth.ValueKind == JsonValueKind.Number)
            {
                layer.DepthMin = layer.DepthMax = depth.GetInt32();
            }
            else if (depth.ValueKind == JsonValueKind.String)
            {
                layer.DepthSpecial = depth.GetString();
            }
            else if (depth.ValueKind == JsonValueKind.Object)
            {
                layer.DepthMin = depth.GetProperty("min").GetInt32();
                layer.DepthMax = depth.GetProperty("max").GetInt32();
            }
        }

        if (elem.TryGetProperty("terrainKind", out var terrainKind))
            layer.TerrainKind = terrainKind.GetString() ?? "";

        return layer;
    }

    private MaterialDistribution ParseMaterialDistribution(JsonElement elem)
    {
        var dist = new MaterialDistribution
        {
            Type = elem.GetProperty("type").GetString() ?? "weighted"
        };

        if (elem.TryGetProperty("materials", out var materials))
        {
            foreach (var mat in materials.EnumerateArray())
            {
                dist.Materials.Add(new MaterialWeight
                {
                    Name = mat.GetProperty("name").GetString() ?? "",
                    Weight = mat.GetProperty("weight").GetSingle()
                });
            }
        }

        return dist;
    }

    /// <summary>
    /// Load geology prototypes from JSON
    /// </summary>
    private async Task<List<GeologyDefinition>> LoadGeologyAsync(string file, Dictionary<string, JsonDocument> schemas)
    {
        // Try the new geology_prototypes.json first
        var prototypeFile = file.Replace("geology.json", "geology_prototypes.json");
        if (!File.Exists(prototypeFile))
        {
            // Fall back to geology.json if it exists
            if (!File.Exists(file))
            {
                ValidationResult.Warnings.Add($"Geology file not found: {prototypeFile} or {file}");
                return new List<GeologyDefinition>();
            }
            prototypeFile = file;
        }

        var json = await File.ReadAllTextAsync(prototypeFile);
        var doc = JsonDocument.Parse(json);

        var prototypes = new List<GeologyDefinition>();

        // Parse prototypes
        if (doc.RootElement.TryGetProperty("prototypes", out var prototypesElem))
        {
            foreach (var elem in prototypesElem.EnumerateArray())
            {
                try
                {
                    var prototype = ParseGeologyDefinition(elem);
                    prototypes.Add(prototype);
                }
                catch (Exception ex)
                {
                    ValidationResult.Errors.Add($"Error parsing geology prototype: {ex.Message}");
                }
            }
        }
        // Handle old format for compatibility
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            Console.WriteLine($"[ContentRegistry] Warning: Using legacy geology.json format");
            // Skip legacy format for now
        }

        // Load into registry
        Geology.LoadPrototypes(prototypes, Version);

        return prototypes;
    }

    private GeologyDefinition ParseGeologyDefinition(JsonElement elem)
    {
        var prototype = new GeologyDefinition
        {
            Id = elem.GetProperty("id").GetString() ?? "",
            Name = elem.GetProperty("name").GetString() ?? "",
            TerrainKind = elem.GetProperty("terrainKind").GetString() ?? "",
            Material = elem.GetProperty("material").GetString() ?? ""
        };

        if (elem.TryGetProperty("navCostBase", out var navCost))
            prototype.NavCostBase = navCost.GetByte();

        if (elem.TryGetProperty("opacity", out var opacity))
            prototype.Opacity = opacity.GetByte();

        if (elem.TryGetProperty("tags", out var tags))
        {
            foreach (var tag in tags.EnumerateArray())
            {
                prototype.Tags.Add(tag.GetString() ?? "");
            }
        }

        return prototype;
    }

    /// <summary>
    /// Load aliases for migration
    /// </summary>
    private async Task LoadAliasesAsync(string file, Dictionary<string, JsonDocument> schemas)
    {
        var json = await File.ReadAllTextAsync(file);
        var doc = JsonDocument.Parse(json);

        // Parse material aliases
        if (doc.RootElement.TryGetProperty("materialAliases", out var materialAliases))
        {
            var aliases = new Dictionary<string, string>();
            foreach (var elem in materialAliases.EnumerateArray())
            {
                var from = elem.GetProperty("from").GetString() ?? "";
                var to = elem.GetProperty("to").GetString() ?? "";
                aliases[from] = to;
            }
            Materials.ApplyAliases(aliases);
        }

        // Additional alias parsing would go here...
    }

    /// <summary>
    /// Cross-validate content references
    /// </summary>
    private void CrossValidateContent()
    {
        // Validate material references in templates
        foreach (var template in BiomeTemplates.GetAllTemplates())
        {
            foreach (var layer in template.Layers)
            {
                if (!string.IsNullOrEmpty(layer.Material))
                {
                    if (!Materials.HasMaterial(layer.Material))
                    {
                        ValidationResult.Warnings.Add(
                            $"Template '{template.Id}' references unknown material '{layer.Material}'");
                    }
                }

                if (layer.MaterialDistribution != null)
                {
                    foreach (var mat in layer.MaterialDistribution.Materials)
                    {
                        if (!Materials.HasMaterial(mat.Name))
                        {
                            ValidationResult.Warnings.Add(
                                $"Template '{template.Id}' references unknown material '{mat.Name}'");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(layer.TerrainKind))
                {
                    if (TerrainKinds.GetKind(layer.TerrainKind) == null)
                    {
                        ValidationResult.Warnings.Add(
                            $"Template '{template.Id}' references unknown terrain kind '{layer.TerrainKind}'");
                    }
                }
            }
        }

        // Validate terrain kind material restrictions
        foreach (var kind in TerrainKinds.GetAllKinds())
        {
            foreach (var matCategory in kind.AllowedMaterials)
            {
                if (matCategory != "*")
                {
                    var hasCategory = Materials.GetMaterialsByCategory(matCategory).Any();
                    if (!hasCategory)
                    {
                        ValidationResult.Warnings.Add(
                            $"Terrain kind '{kind.Name}' allows unknown material category '{matCategory}'");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Compute overall content hash
    /// </summary>
    private void ComputeContentHash()
    {
        var combined = $"{Materials.ContentHash}|{TerrainKinds.Version}|{BiomeTemplates.GetTemplateCount()}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
        var hash = sha256.ComputeHash(bytes);
        ContentHash = BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
    }

    /// <summary>
    /// Get content snapshot for saves
    /// </summary>
    public ContentSnapshot GetSnapshot()
    {
        return new ContentSnapshot
        {
            Version = Version,
            ContentHash = ContentHash,
            MaterialNames = Materials.GetNameToIdSnapshot(),
            LoadedTemplates = BiomeTemplates.GetTemplateIds()
        };
    }

    /// <summary>
    /// Apply content snapshot when loading saves
    /// </summary>
    public void ApplySnapshot(ContentSnapshot snapshot)
    {
        // Validate version compatibility
        if (!Version.IsCompatibleWith(snapshot.Version))
        {
            ValidationResult.Warnings.Add(
                $"Save version {snapshot.Version} may not be fully compatible with content version {Version}");
        }

        // Apply material name mappings if needed
        // This would handle renamed materials
    }
}

/// <summary>
/// Content validation result
/// </summary>
public class ContentValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Content snapshot for saves
/// </summary>
public class ContentSnapshot
{
    public ContentVersion Version { get; set; }
    public string ContentHash { get; set; } = "";
    public Dictionary<string, ushort> MaterialNames { get; set; } = new();
    public List<string> LoadedTemplates { get; set; } = new();
}