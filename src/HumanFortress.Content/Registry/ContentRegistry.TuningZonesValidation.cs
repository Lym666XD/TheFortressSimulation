using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RuntimeZoneDefinitionData = HumanFortress.Contracts.Content.Registry.ZoneDefinitionData;

namespace HumanFortress.Content.Registry;

internal sealed partial class ContentRegistry
{
    private void LoadTuningFiles(string registriesPath)
    {
        if (!Directory.Exists(registriesPath))
        {
            ValidationResult.Errors.Add($"Registries directory not found: {registriesPath}");
            return;
        }

        var files = Directory.GetFiles(registriesPath, "tuning.*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try
            {
                var key = Path.GetFileNameWithoutExtension(file);
                var root = JObject.Parse(File.ReadAllText(file));
                _tuning[key] = root;
            }
            catch (Exception ex)
            {
                ValidationResult.Errors.Add($"Error loading tuning file {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        ContentRegistryDiagnostics.Emit($"[ContentRegistry] Loaded {_tuning.Count} tuning files");
    }

    private void LoadZones(string file)
    {
        if (!File.Exists(file))
        {
            ValidationResult.Warnings.Add($"Zones file not found: {file}");
            return;
        }

        try
        {
            var zoneFile = JsonSerializer.Deserialize<ZoneDefinitionFile>(
                File.ReadAllText(file),
                RuntimeContentJsonOptions);
            if (zoneFile?.Zones == null)
            {
                ValidationResult.Warnings.Add($"Zones file has no zones array: {file}");
                return;
            }

            foreach (var zone in zoneFile.Zones)
            {
                if (zone == null)
                {
                    ValidationResult.Errors.Add($"Failed to parse zone definition in {Path.GetFileName(file)}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(zone.Id))
                {
                    ValidationResult.Errors.Add($"Zone definition missing id in {Path.GetFileName(file)}");
                    continue;
                }

                if (_zones.ContainsKey(zone.Id))
                {
                    ValidationResult.Errors.Add($"Duplicate zone id '{zone.Id}' in {Path.GetFileName(file)}");
                    continue;
                }

                _zones[zone.Id] = zone;
            }

            ContentRegistryDiagnostics.Emit($"[ContentRegistry] Loaded {_zones.Count} zone definitions");
        }
        catch (Exception ex)
        {
            ValidationResult.Errors.Add($"Error loading zones file {Path.GetFileName(file)}: {ex.Message}");
        }
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
            _materials.ApplyAliases(aliases);
        }

        // Additional alias parsing would go here...
    }

    /// <summary>
    /// Cross-validate content references
    /// </summary>
    private void CrossValidateContent()
    {
        // Validate material references in templates
        foreach (var template in _biomeTemplates.GetAllTemplates())
        {
            foreach (var layer in template.Layers)
            {
                if (!string.IsNullOrEmpty(layer.Material))
                {
                    if (!_materials.HasMaterial(layer.Material))
                    {
                        ValidationResult.Warnings.Add(
                            $"Template '{template.Id}' references unknown material '{layer.Material}'");
                    }
                }

                if (layer.MaterialDistribution != null)
                {
                    foreach (var mat in layer.MaterialDistribution.Materials)
                    {
                        if (!_materials.HasMaterial(mat.Name))
                        {
                            ValidationResult.Warnings.Add(
                                $"Template '{template.Id}' references unknown material '{mat.Name}'");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(layer.TerrainKind))
                {
                    if (_terrainKinds.GetKind(layer.TerrainKind) == null)
                    {
                        ValidationResult.Warnings.Add(
                            $"Template '{template.Id}' references unknown terrain kind '{layer.TerrainKind}'");
                    }
                }
            }
        }

        // Validate terrain kind material restrictions
        foreach (var kind in _terrainKinds.GetAllKinds())
        {
            foreach (var matCategory in kind.AllowedMaterials)
            {
                if (matCategory != "*")
                {
                    var hasCategory = _materials.GetMaterialsByCategory(matCategory).Any();
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
        var combined = $"{_materials.ContentHash}|{_terrainKinds.Version}|{_biomeTemplates.GetTemplateCount()}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
        var hash = sha256.ComputeHash(bytes);
        ContentHash = BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
    }

    private sealed class ZoneDefinitionFile
    {
        public List<RuntimeZoneDefinitionData>? Zones { get; set; }
    }
}
