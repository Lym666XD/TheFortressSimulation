using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using JsonFormatting = Newtonsoft.Json.Formatting;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Contracts.Diagnostics;
using RuntimeGeologyData = HumanFortress.Contracts.Content.Registry.GeologyData;
using RuntimeZoneDefinitionData = HumanFortress.Contracts.Content.Registry.ZoneDefinitionData;

namespace HumanFortress.Content.Registry;

/// <summary>
/// Main content registry that manages loaded runtime content.
/// </summary>
internal sealed partial class ContentRegistry : IRuntimeGeologyCatalog
{
    private static readonly JsonSerializerOptions RuntimeContentJsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly MaterialRegistry _materials = new();
    private readonly TerrainKindRegistry _terrainKinds = new();
    private readonly GeologyRegistry _geology = new();
    private readonly BiomeTemplateRegistry _biomeTemplates = new();

    internal IRuntimeMaterialCatalog Materials => _materials;
    internal IRuntimeTerrainKindCatalog TerrainKinds => _terrainKinds;
    internal IConstructionCatalog Constructions => _constructions;
    internal IRecipeCatalog Recipes => _recipes;

    private readonly Dictionary<string, RuntimeGeologyData> _geologyEntries = new();
    private readonly Dictionary<string, RuntimeZoneDefinitionData> _zones = new();
    private readonly Dictionary<string, string[]> _workshopCategoryTags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JObject> _tuning = new();
    private readonly Dictionary<string, ushort> _geologyHandles = new();
    private readonly Dictionary<ushort, string> _handleToGeologyId = new();
    private readonly Dictionary<(string material, string kind), ushort> _geologyByMaterialAndKind = new();
    private ConstructionCatalogStore _constructions = ConstructionCatalogStore.Empty;
    private RecipeCatalogStore _recipes = RecipeCatalogStore.Empty;

    internal IReadOnlyDictionary<string, RuntimeGeologyData> GeologyEntries => _geologyEntries;
    internal IReadOnlyDictionary<string, RuntimeZoneDefinitionData> Zones => _zones;
    internal IReadOnlyDictionary<string, string[]> WorkshopCategoryTags => _workshopCategoryTags;

    internal ContentVersion Version { get; private set; }
    internal string ContentPath { get; private set; } = "";
    internal string ContentHash { get; private set; } = "";
    internal bool IsLoaded { get; private set; }

    // Validation statistics
    internal ContentValidationResult ValidationResult { get; private set; } = new();

    internal ContentRegistry() { }

    /// <summary>
    /// Load all content from the specified path
    /// </summary>
    internal async Task LoadContentAsync(string contentPath)
    {
        ContentRegistryDiagnostics.Emit($"[ContentRegistry] Loading content from: {contentPath}");
        ContentPath = contentPath;
        ContentHash = string.Empty;
        IsLoaded = false;
        ValidationResult = new ContentValidationResult();
        ClearRuntimeContent();

        try
        {
            // Load schemas for validation
            var schemas = await LoadSchemasAsync(Path.Combine(contentPath, "schemas"));
            var registriesPath = Path.Combine(contentPath, "registries");

            LoadTuningFiles(registriesPath);

            // Load and validate materials (prefer authoring format, fall back to registry format)
            var materialsAuthoring = Path.Combine(registriesPath, "materials.authoring.json");
            var materialsRegistry = Path.Combine(registriesPath, "materials.registry.json");
            var materialsLegacy = Path.Combine(registriesPath, "materials.json");

            string materialsFile;
            bool isAuthoringFormat = true;

            if (File.Exists(materialsAuthoring))
            {
                materialsFile = materialsAuthoring;
                ContentRegistryDiagnostics.Emit("[ContentRegistry] Loading materials from authoring format");
            }
            else if (File.Exists(materialsRegistry))
            {
                materialsFile = materialsRegistry;
                isAuthoringFormat = false;
                ContentRegistryDiagnostics.Emit("[ContentRegistry] Loading materials from registry format");
            }
            else
            {
                materialsFile = materialsLegacy;
                ContentRegistryDiagnostics.Emit("[ContentRegistry] Loading materials from legacy format");
            }

            _ = await LoadMaterialsAsync(materialsFile, schemas, isAuthoringFormat);

            // Load and validate terrain kinds
            var terrainKindsFile = Path.Combine(registriesPath, "terrain_kinds.json");
            _ = await LoadTerrainKindsAsync(terrainKindsFile, schemas);

            // Load and validate runtime geology prototypes
            var geologyFile = Path.Combine(registriesPath, "geology.json");
            _ = await LoadGeologyAsync(geologyFile, schemas);

            LoadZones(Path.Combine(registriesPath, "zones.json"));
            LoadWorkshopCategories(Path.Combine(registriesPath, "ui.workshop_categories.json"));

            // Load and validate biome templates
            var biomesPath = Path.Combine(contentPath, "templates", "biomes");
            _ = await LoadBiomeTemplatesAsync(biomesPath, schemas);

            // Load aliases
            var aliasesFile = Path.Combine(registriesPath, "aliases.json");
            if (File.Exists(aliasesFile))
            {
                await LoadAliasesAsync(aliasesFile, schemas);
            }

            AssignGeologyHandles();
            BuildGeologyMaterialKindIndex();

            // Cross-validate references
            CrossValidateContent();

            // Compute overall content hash
            ComputeContentHash();

            IsLoaded = true;
            ContentRegistryDiagnostics.Emit($"[ContentRegistry] Content loaded successfully. Version: {Version}, Hash: {ContentHash}");
            var validationLevel = ValidationResult.Errors.Count > 0
                ? DiagnosticLevel.Error
                : ValidationResult.Warnings.Count > 0
                    ? DiagnosticLevel.Warning
                    : DiagnosticLevel.Information;
            ContentRegistryDiagnostics.Emit($"[ContentRegistry] Validation: {ValidationResult.Warnings.Count} warnings, {ValidationResult.Errors.Count} errors", validationLevel);

            // Print validation errors
            if (ValidationResult.Errors.Count > 0)
            {
                ContentRegistryDiagnostics.Emit("[ContentRegistry] Errors:", DiagnosticLevel.Error);
                foreach (var error in ValidationResult.Errors)
                {
                    ContentRegistryDiagnostics.Emit($"  - {error}", DiagnosticLevel.Error);
                }
            }

            // Print validation warnings
            if (ValidationResult.Warnings.Count > 0)
            {
                ContentRegistryDiagnostics.Emit("[ContentRegistry] Warnings:", DiagnosticLevel.Warning);
                foreach (var warning in ValidationResult.Warnings)
                {
                    ContentRegistryDiagnostics.Emit($"  - {warning}", DiagnosticLevel.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            ContentRegistryDiagnostics.Emit($"[ContentRegistry] Error loading content: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Load content synchronously (for compatibility)
    /// </summary>
    internal void LoadContent(string contentPath)
    {
        LoadContentAsync(contentPath).GetAwaiter().GetResult();
    }

    internal void ApplyCoreData(CoreDataLoadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        _constructions = result.Constructions.Catalog;
        _recipes = result.Recipes.Catalog;
    }

    internal RuntimeZoneDefinitionData? GetZoneDefinition(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _zones.TryGetValue(id, out var zone) ? zone : null;
    }

    internal RuntimeGeologyData? GetGeologyEntry(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _geologyEntries.TryGetValue(id, out var geology) ? geology : null;
    }

    internal RuntimeGeologyData? GetGeologyByHandle(ushort handle)
    {
        return _handleToGeologyId.TryGetValue(handle, out var id) ? GetGeologyEntry(id) : null;
    }

    internal ushort GetGeologyHandle(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _geologyHandles.TryGetValue(id, out var handle) ? handle : (ushort)0;
    }

    internal bool TryGetGeologyHandleByMaterialAndKind(string materialId, string terrainKindName, out ushort handle)
    {
        if (string.IsNullOrWhiteSpace(materialId) || string.IsNullOrWhiteSpace(terrainKindName))
        {
            handle = 0;
            return false;
        }

        return _geologyByMaterialAndKind.TryGetValue((materialId, terrainKindName), out handle);
    }

    internal T? GetTuning<T>(string file, string path) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!_tuning.TryGetValue(file, out var root))
        {
            return default;
        }

        var token = root.SelectToken(path);
        return token?.ToObject<T>();
    }

    internal string? GetTuningJson(string file, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!_tuning.TryGetValue(file, out var root))
        {
            return null;
        }

        var token = root.SelectToken(path);
        return token?.ToString(JsonFormatting.None);
    }

    RuntimeGeologyData? IRuntimeGeologyCatalog.GetGeologyEntry(string id)
    {
        return GetGeologyEntry(id);
    }

    RuntimeGeologyData? IRuntimeGeologyCatalog.GetGeologyByHandle(ushort handle)
    {
        return GetGeologyByHandle(handle);
    }

    ushort IRuntimeGeologyCatalog.GetGeologyHandle(string id)
    {
        return GetGeologyHandle(id);
    }

    bool IRuntimeGeologyCatalog.TryGetGeologyHandleByMaterialAndKind(
        string materialId,
        string terrainKindName,
        out ushort handle)
    {
        return TryGetGeologyHandleByMaterialAndKind(materialId, terrainKindName, out handle);
    }

    private void ClearRuntimeContent()
    {
        _geologyEntries.Clear();
        _zones.Clear();
        _workshopCategoryTags.Clear();
        _tuning.Clear();
        _geologyHandles.Clear();
        _handleToGeologyId.Clear();
        _geologyByMaterialAndKind.Clear();
        _constructions = ConstructionCatalogStore.Empty;
        _recipes = RecipeCatalogStore.Empty;
    }

    /// <summary>
    /// Load schemas for validation
    /// </summary>
    private async Task<Dictionary<string, JsonDocument>> LoadSchemasAsync(string schemasPath)
    {
        var schemas = new Dictionary<string, JsonDocument>();

        if (!Directory.Exists(schemasPath))
        {
            ContentRegistryDiagnostics.Emit($"[ContentRegistry] Warning: Schemas directory not found: {schemasPath}");
            return schemas;
        }

        foreach (var file in Directory.GetFiles(schemasPath, "*.schema.json"))
        {
            var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
            var json = await File.ReadAllTextAsync(file);
            schemas[name] = JsonDocument.Parse(json);
            ContentRegistryDiagnostics.Emit($"[ContentRegistry] Loaded schema: {name}");
        }

        return schemas;
    }

    /// <summary>
    /// Get content snapshot for saves
    /// </summary>
    internal ContentSnapshot GetSnapshot()
    {
        return new ContentSnapshot
        {
            Version = Version,
            ContentHash = ContentHash,
            MaterialNames = _materials.GetNameToIdSnapshot(),
            LoadedTemplates = _biomeTemplates.GetTemplateIds()
        };
    }

    /// <summary>
    /// Apply content snapshot when loading saves
    /// </summary>
    internal void ApplySnapshot(ContentSnapshot snapshot)
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
