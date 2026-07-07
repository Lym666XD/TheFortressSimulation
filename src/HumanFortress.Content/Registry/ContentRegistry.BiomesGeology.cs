using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HumanFortress.Contracts.Content.Registry;
using RuntimeGeologyData = HumanFortress.Contracts.Content.Registry.GeologyData;

namespace HumanFortress.Content.Registry;

internal sealed partial class ContentRegistry
{
    /// <summary>
    /// Load biome templates
    /// </summary>
    private async Task<List<BiomeTemplateDefinition>> LoadBiomeTemplatesAsync(string path, Dictionary<string, JsonDocument> schemas)
    {
        var templates = new List<BiomeTemplateDefinition>();

        if (!Directory.Exists(path))
        {
            ContentRegistryDiagnostics.Emit($"[ContentRegistry] Optional biome templates directory not found, skipping: {path}");
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

        _biomeTemplates.LoadTemplates(templates, Version);
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
    /// Load runtime geology prototypes from JSON.
    /// </summary>
    private async Task<List<GeologyDefinition>> LoadGeologyAsync(string file, Dictionary<string, JsonDocument> schemas)
    {
        if (File.Exists(file))
        {
            return await LoadRuntimeGeologyAsync(file);
        }

        var prototypeFile = file.Replace("geology.json", "geology_prototypes.json", StringComparison.Ordinal);
        if (File.Exists(prototypeFile))
        {
            ValidationResult.Warnings.Add(
                $"Runtime geology file not found: {file}; falling back to prototype-only file {prototypeFile}");
            return await LoadPrototypeGeologyAsync(prototypeFile);
        }

        ValidationResult.Warnings.Add($"Geology file not found: {file} or {prototypeFile}");
        return new List<GeologyDefinition>();
    }

    private async Task<List<GeologyDefinition>> LoadRuntimeGeologyAsync(string file)
    {
        var json = await File.ReadAllTextAsync(file);
        var runtimeEntries = JsonSerializer.Deserialize<List<RuntimeGeologyData>>(json, RuntimeContentJsonOptions)
                             ?? new List<RuntimeGeologyData>();

        var prototypes = new List<GeologyDefinition>(runtimeEntries.Count);
        foreach (var entry in runtimeEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                ValidationResult.Errors.Add($"Geology entry missing id in {Path.GetFileName(file)}");
                continue;
            }

            if (_geologyEntries.ContainsKey(entry.Id))
            {
                ValidationResult.Errors.Add($"Duplicate geology id '{entry.Id}' in {Path.GetFileName(file)}");
                continue;
            }

            _geologyEntries[entry.Id] = entry;
            prototypes.Add(ConvertRuntimeGeologyToPrototype(entry));
        }

        _geology.LoadPrototypes(prototypes, Version);
        ContentRegistryDiagnostics.Emit($"[ContentRegistry] Loaded {_geologyEntries.Count} runtime geology entries");
        return prototypes;
    }

    private async Task<List<GeologyDefinition>> LoadPrototypeGeologyAsync(string file)
    {
        var json = await File.ReadAllTextAsync(file);
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

        // Load into registry
        _geology.LoadPrototypes(prototypes, Version);

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

    private static GeologyDefinition ConvertRuntimeGeologyToPrototype(RuntimeGeologyData entry)
    {
        return new GeologyDefinition
        {
            Id = entry.Id,
            Name = BuildGeologyName(entry.Id),
            TerrainKind = ToStructuredTerrainKindName(entry.TerrainBits.Kind),
            Material = entry.Material,
            NavCostBase = ToOptionalByte(entry.Properties?.NavCostBase),
            Opacity = ToOptionalByte(entry.Properties?.Opacity),
            Tags = new HashSet<string>(entry.Tags)
        };
    }

    private static string BuildGeologyName(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        return id.Replace("core_terrain_", string.Empty, StringComparison.Ordinal)
            .Replace('_', ' ');
    }

    private static byte? ToOptionalByte(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return (byte)Math.Clamp(value.Value, byte.MinValue, byte.MaxValue);
    }

    private static string ToStructuredTerrainKindName(string runtimeKind)
    {
        return runtimeKind switch
        {
            "SolidWall" => "solid_wall",
            "OpenWithFloor" => "open_floor",
            "OpenNoFloor" => "open_space",
            "Ramp" => "ramp",
            "StairsUp" => "stairs_up",
            "StairsDown" => "stairs_down",
            "StairsUD" => "stairs_ud",
            "Slope" => "slope",
            _ => runtimeKind
        };
    }

    private void AssignGeologyHandles()
    {
        _geologyHandles.Clear();
        _handleToGeologyId.Clear();

        ushort handle = 0;
        foreach (var id in _geologyEntries.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            _geologyHandles[id] = handle;
            _handleToGeologyId[handle] = id;
            handle++;
        }
    }

    private void BuildGeologyMaterialKindIndex()
    {
        _geologyByMaterialAndKind.Clear();
        foreach (var (id, geology) in _geologyEntries)
        {
            if (!_geologyHandles.TryGetValue(id, out var handle))
            {
                continue;
            }

            AddGeologyMaterialKindIndex(geology.Material, geology.TerrainBits.Kind, handle);
            AddGeologyMaterialKindIndex(geology.Material, ToStructuredTerrainKindName(geology.TerrainBits.Kind), handle);

            var material = _materials.GetMaterial(geology.Material);
            if (material == null)
            {
                continue;
            }

            AddGeologyMaterialKindIndex(material.Name, geology.TerrainBits.Kind, handle);
            AddGeologyMaterialKindIndex(material.Name, ToStructuredTerrainKindName(geology.TerrainBits.Kind), handle);
            foreach (var alias in material.Aliases)
            {
                AddGeologyMaterialKindIndex(alias, geology.TerrainBits.Kind, handle);
                AddGeologyMaterialKindIndex(alias, ToStructuredTerrainKindName(geology.TerrainBits.Kind), handle);
            }
        }
    }

    private void AddGeologyMaterialKindIndex(string materialId, string terrainKindName, ushort handle)
    {
        if (string.IsNullOrWhiteSpace(materialId) || string.IsNullOrWhiteSpace(terrainKindName))
        {
            return;
        }

        _geologyByMaterialAndKind[(materialId, terrainKindName)] = handle;
    }
}
