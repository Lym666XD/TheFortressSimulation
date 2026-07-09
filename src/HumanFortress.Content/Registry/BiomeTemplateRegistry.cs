using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Content.Registry;

/// <summary>
/// Registry for biome generation templates.
/// </summary>
internal sealed class BiomeTemplateRegistry
{
    private readonly Dictionary<string, BiomeTemplateDefinition> _templatesById = new();
    private readonly List<BiomeTemplateDefinition> _allTemplates = new();
    private readonly List<BiomeTemplateDefinition> _sortedTemplates = new();

    internal ContentVersion Version { get; private set; }

    /// <summary>
    /// Load templates from definitions
    /// </summary>
    internal void LoadTemplates(IEnumerable<BiomeTemplateDefinition> templates, ContentVersion version)
    {
        Version = version;
        Clear();

        // Add all templates
        foreach (var template in templates)
        {
            AddTemplate(template);
        }

        // Sort by priority (higher priority first)
        _sortedTemplates.AddRange(_allTemplates
            .OrderByDescending(static template => template.Priority)
            .ThenBy(static template => template.Id, StringComparer.Ordinal));

        ContentRegistryDiagnostics.Emit($"[BiomeTemplateRegistry] Loaded {_templatesById.Count} biome templates, version {version}");
    }

    /// <summary>
    /// Add a template to the registry
    /// </summary>
    private void AddTemplate(BiomeTemplateDefinition template)
    {
        // Validate
        template.Validate();

        // Check for duplicate ID
        if (_templatesById.ContainsKey(template.Id))
        {
            throw new InvalidOperationException($"Duplicate biome template ID: '{template.Id}'");
        }

        // Add to registries
        _templatesById[template.Id] = template;
        _allTemplates.Add(template);
    }

    /// <summary>
    /// Get template by ID
    /// </summary>
    internal BiomeTemplateDefinition? GetTemplate(string id)
    {
        return _templatesById.GetValueOrDefault(id);
    }

    /// <summary>
    /// Select best matching template for given parameters
    /// </summary>
    internal BiomeTemplateDefinition? SelectTemplate(BiomeParameters parameters)
    {
        // Find all matching templates
        var matches = _sortedTemplates
            .Where(t => t.Conditions.Matches(parameters))
            .ToList();

        if (matches.Count == 0)
        {
            ContentRegistryDiagnostics.Emit("[BiomeTemplateRegistry] No template matches parameters");
            return GetDefaultTemplate();
        }

        // Return highest priority match
        return matches[0];
    }

    /// <summary>
    /// Select template by explicit ID with fallback
    /// </summary>
    internal BiomeTemplateDefinition SelectTemplateById(string id)
    {
        if (_templatesById.TryGetValue(id, out var template))
        {
            return template;
        }

        ContentRegistryDiagnostics.Emit($"[BiomeTemplateRegistry] Warning: Template '{id}' not found, using default");
        return GetDefaultTemplate() ?? throw new InvalidOperationException("No default template available");
    }

    /// <summary>
    /// Get templates for a specific climate
    /// </summary>
    internal IEnumerable<BiomeTemplateDefinition> GetTemplatesForClimate(float temperature, float rainfall)
    {
        return _allTemplates.Where(t =>
            (t.Conditions.Temperature == null || t.Conditions.Temperature.Value.Contains(temperature)) &&
            (t.Conditions.Rainfall == null || t.Conditions.Rainfall.Value.Contains(rainfall)))
            .OrderByDescending(static template => template.Priority)
            .ThenBy(static template => template.Id, StringComparer.Ordinal);
    }

    /// <summary>
    /// Get default/fallback template
    /// </summary>
    private BiomeTemplateDefinition? GetDefaultTemplate()
    {
        // Try to find a template marked as default
        var defaultTemplate = _allTemplates.FirstOrDefault(t => t.Id == "default" || t.Id == "plains");
        if (defaultTemplate != null)
            return defaultTemplate;

        // Return first template if any exist
        return _allTemplates.FirstOrDefault();
    }

    /// <summary>
    /// Get all template IDs for save snapshot
    /// </summary>
    internal List<string> GetTemplateIds()
    {
        return _templatesById.Keys
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Get template count
    /// </summary>
    internal int GetTemplateCount() => _templatesById.Count;

    /// <summary>
    /// Get all templates
    /// </summary>
    internal IEnumerable<BiomeTemplateDefinition> GetAllTemplates() => _allTemplates
        .OrderBy(static template => template.Id, StringComparer.Ordinal);

    /// <summary>
    /// Clear the registry
    /// </summary>
    private void Clear()
    {
        _templatesById.Clear();
        _allTemplates.Clear();
        _sortedTemplates.Clear();
    }
}
